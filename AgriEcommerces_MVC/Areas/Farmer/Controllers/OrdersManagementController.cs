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

            var list = await _db.orderdetails
                                 .Include(d => d.order)
                                 .Include(d => d.product)
                                 .Where(d => d.sellerid == farmerId)
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

            if (order.status == "Chờ duyệt")
            {
                order.status = "Đã duyệt";
                TempData["Success"] = "Đơn hàng đã được duyệt thành công!";
            }
            else if (order.status == "Đã duyệt")
            {
                order.status = "Đang vận chuyển";
                TempData["Success"] = "Đơn hàng đã được chuyển sang trạng thái 'Đang vận chuyển'!";
            }
            else
            {
                TempData["Error"] = "Không thể thay đổi trạng thái đơn hàng trong trạng thái hiện tại.";
                return RedirectToAction("Index");
            }

            _db.orders.Update(order);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

    }
}
