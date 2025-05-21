using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

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
                              .ToList();
            return View(products);
        }

        public IActionResult Payment()
        {
            var cart = HttpContext.Session.GetObject<CartViewModel>(CART_KEY) ?? new CartViewModel();
            if (cart.Items == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                return RedirectToAction("Index");
            }
            var model = new PaymentViewModel
            {
                Cart = cart
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(PaymentViewModel model)
        {
            // 1) Lấy giỏ hàng từ Session
            var cart = HttpContext.Session
                          .GetObject<CartViewModel>(CART_KEY)
                       ?? new CartViewModel();                           
            model.Cart = cart;

            // 2) Kiểm tra form nhập (tỉnh, huyện, xã, địa chỉ)
            ModelState.Remove(nameof(model.Cart));

            if (!ModelState.IsValid)
            {
                return View(model);
            }   

            // 3) Lấy userId từ Claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Challenge();
            var userId = int.Parse(userIdClaim.Value);

            // 4) Tạo đối tượng order, gán shippingaddress từ model
            var order = new order
            {
                
                customerid = userId,
                customername = model.customername,
                customerphone = model.customerphone,
                orderdate = DateTime.Now,
                totalamount = cart.GrandTotal,
                status = "Chờ duyệt",
                shippingaddress = $"{model.address}, {model.ward}, {model.district}, {model.province}"
            };

            // 5) Sinh chi tiết đơn từ cart.Items
            order.orderdetails = new List<orderdetail>();
            foreach (var item in cart.Items)
            {
                // Lấy seller của sản phẩm
                var prod = await _db.products
                             .AsNoTracking()
                             .FirstOrDefaultAsync(p => p.productid == item.ProductId);

                order.orderdetails.Add(new orderdetail
                {
                    productid = item.ProductId,
                    quantity = item.Quantity,
                    unitprice = item.UnitPrice,
                    sellerid = prod.userid   // gán farmerId
                });
            }

            // 6) Lưu order cùng orderdetails vào DB
            _db.orders.Add(order);
            await _db.SaveChangesAsync();

            // 7) Xóa session Cart và redirect
            HttpContext.Session.Remove(CART_KEY);

            return RedirectToAction("OrderConfirmation", new { orderId = order.orderid });
        }



        public IActionResult OrderConfirmation(int orderId)
        {
            var order = _db.orders
                .Include(o => o.orderdetails)
                .ThenInclude(od => od.product)
                .FirstOrDefault(o => o.orderid == orderId);

            return View(order);
        }  
    }
}