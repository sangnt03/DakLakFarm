using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace AgriEcommerces_MVC.Controllers
{
    public class HomeController : Controller
    {
        private const string CART_KEY = "Cart";
        private readonly ApplicationDbContext _db;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext db, ILogger<HomeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var categories = _db.categories.ToList();
            ViewBag.Categories = categories;

            var products = _db.products
                              .Include(p => p.productimages)
                              .Include(p => p.reviews)
                              .ToList();
            return View(products);
        }

        // GET: Payment page for either ALL items or 1 nhóm seller
        [Authorize]
        public IActionResult Payment(int? sellerId)
        {
            // 1) Lấy giỏ hàng từ Session
            var cartAll = HttpContext.Session
                                 .GetObject<CartViewModel>(CART_KEY)
                           ?? new CartViewModel();

            if (!cartAll.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index");
            }

            // 2) Lọc theo sellerId nếu có
            CartViewModel cartForPayment;
            if (sellerId.HasValue)
            {
                cartForPayment = new CartViewModel
                {
                    Items = cartAll.Items
                                   .Where(i => i.SellerId == sellerId.Value)
                                   .ToList()
                };
                if (!cartForPayment.Items.Any())
                {
                    TempData["Error"] = "Không tìm thấy sản phẩm của người bán này trong giỏ hàng.";
                    return RedirectToAction("Index");
                }
            }
            else
            {
                cartForPayment = cartAll;
            }

            // 3) Đẩy vào ViewModel
            var model = new PaymentViewModel
            {
                Cart = cartForPayment,
                SellerId = sellerId
            };
            return View(model);
        }

        [Authorize]
        public IActionResult OrderConfirmation(int orderId)
        {
            var order = _db.orders
                .Include(o => o.orderdetails)
                .ThenInclude(od => od.product)
                .FirstOrDefault(o => o.orderid == orderId);

            return View(order);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();

            int userId = int.Parse(userIdClaim.Value);

            var user = await _db.users.AsNoTracking().FirstOrDefaultAsync(u => u.userid == userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [Authorize]
        public async Task<IActionResult> MyOrders()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();
            int userId = int.Parse(userIdClaim.Value);

            var orders = await _db.orders
                .Where(o => o.customerid == userId)
                .Include(o => o.orderdetails)
                    .ThenInclude(od => od.product)
                        .ThenInclude(p => p.reviews)
                .Include(o => o.orderdetails)
                    .ThenInclude(od => od.product)
                        .ThenInclude(p => p.productimages)
                .OrderByDescending(o => o.orderdate)
                .ToListAsync();

            return View(orders);
        }

        [Authorize]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();
            int userId = int.Parse(userIdClaim.Value);

            var order = await _db.orders
                .Where(o => o.orderid == id && o.customerid == userId)
                .Include(o => o.orderdetails)
                    .ThenInclude(od => od.product)
                        .ThenInclude(p => p.productimages)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int orderid)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();
            int userId = int.Parse(userIdClaim.Value);

            // Lấy đơn hàng, chi tiết và thông tin thanh toán
            var order = await _db.orders
                .Include(o => o.orderdetails)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.orderid == orderid && o.customerid == userId);

            if (order == null) return NotFound();

            
            if (order.status != "Shipped" && order.status != "Processing" && order.status != "Pending")
            {
                TempData["Error"] = "Trạng thái đơn hàng không hợp lệ để xác nhận.";
                return RedirectToAction("MyOrders");
            }

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Chuyển trạng thái thành "Delivered" (Đã nhận hàng)
                order.status = "Delivered";

                var codPayment = order.Payments?.FirstOrDefault(p => p.PaymentMethod == "COD" && p.Status == "Pending");
                if (codPayment != null)
                {
                    codPayment.Status = "Success";
                    codPayment.CreateDate = DateTimeHelper.GetVietnamTime();
                    _db.Payments.Update(codPayment);
                }

                foreach (var detail in order.orderdetails)
                {
                    if (detail.FarmerRevenue > 0)
                    {
                        var walletTransaction = new WalletTransaction
                        {
                            FarmerId = detail.sellerid,
                            Amount = detail.FarmerRevenue,
                            Type = "OrderRevenue",
                            ReferenceId = detail.orderdetailid,
                            Description = $"Doanh thu đơn hàng {order.ordercode}",
                            CreateDate = DateTimeHelper.GetVietnamTime()
                        };

                        _db.WalletTransaction.Add(walletTransaction);
                    }
                }
                _db.orders.Update(order);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Xác nhận thành công! Cảm ơn bạn đã mua sắm.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi ConfirmReceived");
                TempData["Error"] = "Có lỗi xảy ra.";
            }

            return RedirectToAction("MyOrders");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();

            int userId = int.Parse(userIdClaim.Value);

            var user = await _db.users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.userid == userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(int userid, string? fullname, string? phonenumber)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();

            int currentUserId = int.Parse(userIdClaim.Value);

            if (currentUserId != userid) return Forbid();

            var userInDb = await _db.users.FirstOrDefaultAsync(u => u.userid == userid);
            if (userInDb == null) return NotFound();

            userInDb.fullname = string.IsNullOrWhiteSpace(fullname) ? null : fullname.Trim();
            userInDb.phonenumber = string.IsNullOrWhiteSpace(phonenumber) ? null : phonenumber.Trim();

            if (string.IsNullOrWhiteSpace(userInDb.fullname))
            {
                ModelState.AddModelError(nameof(fullname), "Họ và tên không được để trống.");
            }
            if (!string.IsNullOrEmpty(userInDb.phonenumber) && !userInDb.phonenumber.All(char.IsDigit))
            {
                ModelState.AddModelError(nameof(phonenumber), "Số điện thoại chỉ được chứa chữ số.");
            }

            if (!ModelState.IsValid)
            {
                return View(userInDb);
            }

            try
            {
                _db.users.Update(userInDb);
                await _db.SaveChangesAsync();
                TempData["SuccessEdit"] = "Cập nhật thông tin thành công!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật user với userid={UserId}", userid);
                TempData["ErrorEdit"] = "Đã xảy ra lỗi khi lưu thông tin. Vui lòng thử lại sau.";
            }

            return RedirectToAction(nameof(EditProfile));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        public IActionResult GioiThieu()
        {
            return View();
        }
        public IActionResult ThuongHieu()
        {
            return View();
        }

        public IActionResult Blog()
        {
            return View();
        }
    }
}