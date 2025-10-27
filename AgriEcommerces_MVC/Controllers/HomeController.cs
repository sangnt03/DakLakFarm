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

        // POST: Xử lý thanh toán
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(PaymentViewModel model)
        {
            // 1) Lấy giỏ hàng gốc
            var cartAll = HttpContext.Session
                                 .GetObject<CartViewModel>(CART_KEY)
                           ?? new CartViewModel();

            // 2) Gán nhóm cần thanh toán lại
            model.Cart = model.SellerId.HasValue
                ? new CartViewModel
                {
                    Items = cartAll.Items
                                   .Where(i => i.SellerId == model.SellerId.Value)
                                   .ToList()
                }
                : cartAll;

            // 3) Validate
            ModelState.Remove(nameof(model.Cart));
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 4) Lấy userId từ Claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();
            var userId = int.Parse(userIdClaim.Value);

            // 5) Tạo order
            var order = new order
            {
                customerid = userId,
                customername = model.customername,
                customerphone = model.customerphone,
                orderdate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                totalamount = model.Cart.GrandTotal,
                status = "Chờ duyệt",
                shippingaddress = $"{model.address}, {model.ward}, {model.district}, {model.province}"
            };

            // 6) Tạo orderdetails chỉ cho nhóm này
            order.orderdetails = new List<orderdetail>();
            foreach (var item in model.Cart.Items)
            {
                var prod = await _db.products
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.productid == item.ProductId);
                order.orderdetails.Add(new orderdetail
                {
                    productid = item.ProductId,
                    quantity = item.Quantity,
                    unitprice = item.UnitPrice,
                    sellerid = prod.userid
                });
            }

            // 7) Lưu
            _db.orders.Add(order);
            await _db.SaveChangesAsync();

            // 8) Cập nhật lại session: xoá nhóm đã thanh toán
            var remainingItems = cartAll.Items
                                        .Where(i => !model.SellerId.HasValue
                                                 || i.SellerId != model.SellerId.Value)
                                        .ToList();
            if (remainingItems.Any())
            {
                cartAll.Items = remainingItems;
                HttpContext.Session.SetObject(CART_KEY, cartAll);
            }
            else
            {
                HttpContext.Session.Remove(CART_KEY);
            }

            // 9) Redirect confirmation
            return RedirectToAction("OrderConfirmation", new { orderId = order.orderid });
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
            // 1) Lấy userId từ Claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Challenge();

            int userId = int.Parse(userIdClaim.Value);

            // 2) Lấy thông tin user từ DB
            var user = await _db.users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.userid == userId);
            if (user == null)
                return NotFound();

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
                        .ThenInclude(p => p.productimages)
                .OrderByDescending(o => o.orderdate)
                .ToListAsync();

            return View(orders);
        }

        // 2) Chi tiết một đơn
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

            var order = await _db.orders
                .FirstOrDefaultAsync(o => o.orderid == orderid && o.customerid == userId);

            if (order == null) return NotFound();

            // Cập nhật trạng thái
            if (order.status == "Đang vận chuyển" || order.status == "Đã duyệt")
            {
                order.status = "Đã nhận hàng";
                await _db.SaveChangesAsync();
                TempData["Success"] = "Đã xác nhận nhận hàng thành công!";
            }
            else
            {
                TempData["Error"] = "Đơn hàng không thể xác nhận trong trạng thái hiện tại.";
            }

            return RedirectToAction("MyOrders");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            // 1) Lấy userId từ claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Challenge(); // redirect đến login

            int userId = int.Parse(userIdClaim.Value);

            // 2) Lấy thông tin user từ DB
            var user = await _db.users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(u => u.userid == userId);
            if (user == null)
                return NotFound();

            return View(user);
        }

        // POST: /Home/EditProfile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(int userid, string? fullname, string? phonenumber)
        {
            // 1) Xác định user hiện tại
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Challenge();

            int currentUserId = int.Parse(userIdClaim.Value);

            // 2) Ngăn không cho sửa user khác
            if (currentUserId != userid)
                return Forbid();

            // 3) Lấy user từ DB
            var userInDb = await _db.users.FirstOrDefaultAsync(u => u.userid == userid);
            if (userInDb == null)
                return NotFound();

            // 4) Cập nhật các trường được phép sửa
            userInDb.fullname = string.IsNullOrWhiteSpace(fullname)
                                ? null
                                : fullname.Trim();
            userInDb.phonenumber = string.IsNullOrWhiteSpace(phonenumber)
                                   ? null
                                   : phonenumber.Trim();

            // 5) Validate dữ liệu
            if (string.IsNullOrWhiteSpace(userInDb.fullname))
            {
                ModelState.AddModelError(nameof(fullname), "Họ và tên không được để trống.");
            }
            if (!string.IsNullOrEmpty(userInDb.phonenumber)
                && !userInDb.phonenumber.All(char.IsDigit))
            {
                ModelState.AddModelError(nameof(phonenumber), "Số điện thoại chỉ được chứa chữ số.");
            }

            if (!ModelState.IsValid)
            {
                // Có lỗi validate, hiển thị lại view với các lỗi
                return View(userInDb);
            }

            // 6) Lưu vào DB
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

            // 7) Redirect về GET EditProfile để hiển thị lại với message
            return RedirectToAction(nameof(EditProfile));
        }

        // GET: /Home/ChangePassword
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
