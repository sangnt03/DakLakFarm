using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Authorize(Roles = "Admin")]
    public class ManagerPromotionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManagerPromotionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Management/ManagerPromotion/Index
        public async Task<IActionResult> Index()
        {
            var promotions = await _context.promotions
                                           .Include(p => p.CreatedBy)
                                           .OrderByDescending(p => p.CreatedAt)
                                           .ToListAsync();
            return View(promotions);
        }

        // GET: /Management/ManagerPromotion/Create
        public async Task<IActionResult> Create()
        {
            await LoadViewData();
            return View();
        }

        // POST: /Management/ManagerPromotion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Code,Name,Description,DiscountType,DiscountValue,MaxDiscountAmount,MinOrderValue,MaxUsagePerUser,TotalUsageLimit,ApplicableTo,TargetCustomerType,StartDate,EndDate,IsActive")]
            promotion newPromotion,
            int[] selectedProducts,
            int[] selectedCategories,
            int[] selectedFarmers)
        {
            // Loại bỏ ModelState error cho Navigation Properties
            ModelState.Remove("CreatedBy");
            ModelState.Remove("PromotionProducts");
            ModelState.Remove("PromotionCategories");
            ModelState.Remove("PromotionFarmers");
            ModelState.Remove("PromotionUsageHistory");

            // Chuyển DateTime sang UTC
            newPromotion.StartDate = ConvertToUtc(newPromotion.StartDate);
            newPromotion.EndDate = ConvertToUtc(newPromotion.EndDate);

            // Xử lý code
            if (!string.IsNullOrEmpty(newPromotion.Code))
            {
                newPromotion.Code = newPromotion.Code.ToUpper().Trim();
                bool codeExists = await _context.promotions.AnyAsync(p => p.Code == newPromotion.Code);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Mã khuyến mãi này đã tồn tại.");
                }
            }

            // Validate ngày
            if (newPromotion.EndDate <= newPromotion.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            // Validate discount value
            if (newPromotion.DiscountType == "percentage" && newPromotion.DiscountValue > 100)
            {
                ModelState.AddModelError("DiscountValue", "Giá trị phần trăm không được vượt quá 100%.");
            }

            if (newPromotion.DiscountValue <= 0)
            {
                ModelState.AddModelError("DiscountValue", "Giá trị giảm giá phải lớn hơn 0.");
            }

            // Validate applicable scope
            if (newPromotion.ApplicableTo == "specific_products" && (selectedProducts == null || selectedProducts.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một sản phẩm khi áp dụng cho sản phẩm cụ thể.");
            }

            if (newPromotion.ApplicableTo == "specific_categories" && (selectedCategories == null || selectedCategories.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một danh mục khi áp dụng cho danh mục cụ thể.");
            }

            if (newPromotion.ApplicableTo == "specific_farmers" && (selectedFarmers == null || selectedFarmers.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một farmer khi áp dụng cho farmer cụ thể.");
            }

            if (ModelState.IsValid)
            {
                var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                newPromotion.CreatedAt = DateTime.UtcNow;
                newPromotion.CurrentUsageCount = 0;
                newPromotion.CreatedByUserId = int.Parse(adminUserId);

                _context.Add(newPromotion);
                await _context.SaveChangesAsync();

                // Thêm quan hệ với products/categories/farmers
                await AddPromotionRelations(newPromotion.PromotionId, selectedProducts, selectedCategories, selectedFarmers);

                TempData["SuccessMessage"] = $"Đã tạo mới khuyến mãi '{newPromotion.Name}' thành công.";
                return RedirectToAction(nameof(Index));
            }

            await LoadViewData();
            return View(newPromotion);
        }

        // GET: /Management/ManagerPromotion/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.promotions
                .Include(p => p.CreatedBy)
                .Include(p => p.PromotionProducts).ThenInclude(pp => pp.Product)
                .Include(p => p.PromotionCategories).ThenInclude(pc => pc.Category)
                .Include(p => p.PromotionFarmers).ThenInclude(pf => pf.Farmer)
                .Include(p => p.PromotionUsageHistory).ThenInclude(h => h.User)
                .FirstOrDefaultAsync(m => m.PromotionId == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // GET: /Management/ManagerPromotion/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.promotions
                .Include(p => p.PromotionProducts)
                .Include(p => p.PromotionCategories)
                .Include(p => p.PromotionFarmers)
                .FirstOrDefaultAsync(p => p.PromotionId == id);

            if (promotion == null)
            {
                return NotFound();
            }

            await LoadViewData();

            // Pass selected items to view
            ViewBag.SelectedProducts = promotion.PromotionProducts?.Select(pp => pp.ProductId).ToArray() ?? Array.Empty<int>();
            ViewBag.SelectedCategories = promotion.PromotionCategories?.Select(pc => pc.CategoryId).ToArray() ?? Array.Empty<int>();
            ViewBag.SelectedFarmers = promotion.PromotionFarmers?.Select(pf => pf.FarmerId).ToArray() ?? Array.Empty<int>();

            return View(promotion);
        }

        // POST: /Management/ManagerPromotion/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("PromotionId,Code,Name,Description,DiscountType,DiscountValue,MaxDiscountAmount,MinOrderValue,MaxUsagePerUser,TotalUsageLimit,CurrentUsageCount,ApplicableTo,TargetCustomerType,StartDate,EndDate,IsActive,CreatedByUserId,CreatedAt")]
            promotion promotion,
            int[] selectedProducts,
            int[] selectedCategories,
            int[] selectedFarmers)
        {
            if (id != promotion.PromotionId)
            {
                return NotFound();
            }

            // Loại bỏ ModelState error cho Navigation Properties
            ModelState.Remove("CreatedBy");
            ModelState.Remove("PromotionProducts");
            ModelState.Remove("PromotionCategories");
            ModelState.Remove("PromotionFarmers");
            ModelState.Remove("PromotionUsageHistory");

            // Chuyển DateTime sang UTC
            promotion.StartDate = ConvertToUtc(promotion.StartDate);
            promotion.EndDate = ConvertToUtc(promotion.EndDate);
            promotion.CreatedAt = ConvertToUtc(promotion.CreatedAt);

            // Validate code uniqueness (exclude current promotion)
            if (!string.IsNullOrEmpty(promotion.Code))
            {
                promotion.Code = promotion.Code.ToUpper().Trim();
                bool codeExists = await _context.promotions
                    .AnyAsync(p => p.Code == promotion.Code && p.PromotionId != promotion.PromotionId);
                if (codeExists)
                {
                    ModelState.AddModelError("Code", "Mã khuyến mãi này đã tồn tại.");
                }
            }

            // Validate dates
            if (promotion.EndDate <= promotion.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            }

            // Validate discount value
            if (promotion.DiscountType == "percentage" && promotion.DiscountValue > 100)
            {
                ModelState.AddModelError("DiscountValue", "Giá trị phần trăm không được vượt quá 100%.");
            }

            if (promotion.DiscountValue <= 0)
            {
                ModelState.AddModelError("DiscountValue", "Giá trị giảm giá phải lớn hơn 0.");
            }

            // Validate applicable scope
            if (promotion.ApplicableTo == "specific_products" && (selectedProducts == null || selectedProducts.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một sản phẩm khi áp dụng cho sản phẩm cụ thể.");
            }

            if (promotion.ApplicableTo == "specific_categories" && (selectedCategories == null || selectedCategories.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một danh mục khi áp dụng cho danh mục cụ thể.");
            }

            if (promotion.ApplicableTo == "specific_farmers" && (selectedFarmers == null || selectedFarmers.Length == 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một farmer khi áp dụng cho farmer cụ thể.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Remove old relations
                    var oldProducts = await _context.promotion_products
                        .Where(pp => pp.PromotionId == id).ToListAsync();
                    _context.promotion_products.RemoveRange(oldProducts);

                    var oldCategories = await _context.promotion_categories
                        .Where(pc => pc.PromotionId == id).ToListAsync();
                    _context.promotion_categories.RemoveRange(oldCategories);

                    var oldFarmers = await _context.promotion_farmers
                        .Where(pf => pf.PromotionId == id).ToListAsync();
                    _context.promotion_farmers.RemoveRange(oldFarmers);

                    // Update promotion
                    _context.Update(promotion);
                    await _context.SaveChangesAsync();

                    // Add new relations
                    await AddPromotionRelations(promotion.PromotionId, selectedProducts, selectedCategories, selectedFarmers);

                    TempData["SuccessMessage"] = $"Đã cập nhật khuyến mãi '{promotion.Name}' thành công.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PromotionExists(promotion.PromotionId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            await LoadViewData();
            ViewBag.SelectedProducts = selectedProducts ?? Array.Empty<int>();
            ViewBag.SelectedCategories = selectedCategories ?? Array.Empty<int>();
            ViewBag.SelectedFarmers = selectedFarmers ?? Array.Empty<int>();

            return View(promotion);
        }

        // GET: /Management/ManagerPromotion/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var promotion = await _context.promotions
                .Include(p => p.CreatedBy)
                .Include(p => p.PromotionUsageHistory)
                .FirstOrDefaultAsync(m => m.PromotionId == id);

            if (promotion == null)
            {
                return NotFound();
            }

            return View(promotion);
        }

        // POST: /Management/ManagerPromotion/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var promotion = await _context.promotions
                .Include(p => p.PromotionUsageHistory)
                .FirstOrDefaultAsync(p => p.PromotionId == id);

            if (promotion == null)
            {
                return NotFound();
            }

            // Check if promotion has been used
            if (promotion.PromotionUsageHistory != null && promotion.PromotionUsageHistory.Any())
            {
                TempData["ErrorMessage"] = "Không thể xóa khuyến mãi đã được sử dụng. Vui lòng vô hiệu hóa thay vì xóa.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.promotions.Remove(promotion);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa khuyến mãi '{promotion.Name}' thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra khi xóa khuyến mãi: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        #region Helper Methods

        /// Convert DateTime to UTC if it's Unspecified
        private DateTime ConvertToUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }
            else if (dateTime.Kind == DateTimeKind.Local)
            {
                return dateTime.ToUniversalTime();
            }
            return dateTime;
        }

        /// Add promotion relations (products/categories/farmers)
        private async Task AddPromotionRelations(int promotionId, int[]? products, int[]? categories, int[]? farmers)
        {
            if (products != null && products.Length > 0)
            {
                foreach (var productId in products)
                {
                    _context.promotion_products.Add(new promotion_product
                    {
                        PromotionId = promotionId,
                        ProductId = productId
                    });
                }
            }

            if (categories != null && categories.Length > 0)
            {
                foreach (var categoryId in categories)
                {
                    _context.promotion_categories.Add(new promotion_category
                    {
                        PromotionId = promotionId,
                        CategoryId = categoryId
                    });
                }
            }

            if (farmers != null && farmers.Length > 0)
            {
                foreach (var farmerId in farmers)
                {
                    _context.promotion_farmers.Add(new promotion_farmer
                    {
                        PromotionId = promotionId,
                        FarmerId = farmerId
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        /// Load data for dropdowns
        private async Task LoadViewData()
        {
            ViewBag.Products = await _context.products
                .Where(p => p.quantityavailable > 0)
                .OrderBy(p => p.productname)
                .Select(p => new SelectListItem
                {
                    Value = p.productid.ToString(),
                    Text = p.productname
                }).ToListAsync();

            ViewBag.Categories = await _context.categories
                .OrderBy(c => c.categoryname)
                .Select(c => new SelectListItem
                {
                    Value = c.categoryid.ToString(),
                    Text = c.categoryname
                }).ToListAsync();

            ViewBag.Farmers = await _context.users
                .Where(u => u.role == "Farmer" && u.isapproved == true)
                .OrderBy(u => u.shop_name ?? u.fullname ?? u.email)
                .Select(u => new SelectListItem
                {
                    Value = u.userid.ToString(),
                    Text = u.shop_name ?? u.fullname ?? u.email
                }).ToListAsync();
        }

        /// Check if promotion exists
        private bool PromotionExists(int id)
        {
            return _context.promotions.Any(e => e.PromotionId == id);
        }

        #endregion
    }
}