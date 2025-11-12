using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Cần để lấy ID của Admin

namespace AgriEcommerces_MVC.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] 
    public class ManagerPromotionController : Controller
    {
        private readonly ApplicationDbContext _context;

        // "Tiêm" DbContext vào
        public ManagerPromotionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Promotions
        public async Task<IActionResult> Index()
        {
            // Lấy tất cả khuyến mãi, sắp xếp theo cái mới nhất
            var promotions = await _context.promotions
                                           .OrderByDescending(p => p.CreatedAt)
                                           .ToListAsync();
            return View(promotions);
        }

        // GET: /Admin/Promotions/Create
        public IActionResult Create()
        {
            // Chỉ trả về 1 View trống để Admin nhập liệu
            return View();
        }

        // POST: /Admin/Promotions/Create
        [HttpPost]
        [ValidateAntiForgeryToken] // Chống tấn công CSRF
        public async Task<IActionResult> Create(
            // [Bind] để chỉ định rõ các trường được phép POST lên
            [Bind("code,name,description,discounttype,discountvalue,maxdiscountamount,minordervalue,maxusageperuser,totalusagelimit,applicableto,targetcustomertype,startdate,enddate,isactive")]
            promotion newPromotion)
        {
            // Xử lý lỗi trùng code (viết hoa/thường)
            if (!string.IsNullOrEmpty(newPromotion.Code))
            {
                newPromotion.Code = newPromotion.Code.ToUpper().Trim();
                bool codeExists = await _context.promotions.AnyAsync(p => p.Code == newPromotion.Code);
                if (codeExists)
                {
                    ModelState.AddModelError("code", "Mã khuyến mãi này đã tồn tại.");
                }
            }

            if (ModelState.IsValid)
            {
                // Lấy UserID của Admin đang đăng nhập
                var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Gán các giá trị mặc định
                newPromotion.CreatedAt = DateTime.UtcNow;
                newPromotion.CurrentUsageCount = 0;
                // Cần xử lý lỗi nếu UserID không phải là số
                newPromotion.CreatedByUserId = int.Parse(adminUserId);

                _context.Add(newPromotion);
                await _context.SaveChangesAsync();

                // TempData để hiển thị thông báo thành công ở trang Index
                TempData["SuccessMessage"] = $"Đã tạo mới khuyến mãi '{newPromotion.Name}' thành công.";

                // Chuyển hướng về trang danh sách
                return RedirectToAction(nameof(Index));
            }

            // Nếu model không hợp lệ, trả lại View với dữ liệu đã nhập
            return View(newPromotion);
        }
    }
}