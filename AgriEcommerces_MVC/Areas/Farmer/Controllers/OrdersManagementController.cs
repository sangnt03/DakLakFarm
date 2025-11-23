using AgriEcommerces_MVC.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class OrdersManagementController : Controller
    {
        private readonly ApplicationDbContext _db;

        public OrdersManagementController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Sắp xếp đơn mới nhất lên đầu
            var list = await _db.orderdetails
                                 .Include(d => d.order)
                                 .Include(d => d.product)
                                 .Where(d => d.sellerid == farmerId)
                                 .OrderByDescending(d => d.order.orderdate)
                                 .ToListAsync();

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var detail = await _db.orderdetails
                                  .AsNoTracking()
                                  .SingleOrDefaultAsync(d => d.orderdetailid == id);

            if (detail == null || detail.sellerid != farmerId)
                return Forbid();

            var order = await _db.orders
                                 .SingleOrDefaultAsync(o => o.orderid == detail.orderid);

            if (order == null)
                return NotFound();

            if (order.status == "Pending" || order.status == "Paid")
            {
                order.status = "Processing";
                TempData["Success"] = "Đã xác nhận đơn hàng (Processing). Vui lòng chuẩn bị hàng.";
            }
            // 2. Từ Processing -> Shipped
            // -> Chuyển sang Shipped (Đã giao cho đơn vị vận chuyển)
            else if (order.status == "Processing")
            {
                order.status = "Shipped";
                TempData["Success"] = "Đơn hàng đã được giao cho vận chuyển (Shipped).";
            }
            else
            {
                TempData["Error"] = $"Không thể thay đổi trạng thái từ '{order.status}'.";
                return RedirectToAction("Index");
            }

            _db.orders.Update(order);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}