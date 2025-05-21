using AgriEcommerces_MVC.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(Roles = "Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class OrdersManagementController : Controller
    {
            private readonly ApplicationDbContext _db;
            public OrdersManagementController(ApplicationDbContext db) => _db = db;

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
            // Lấy detail trước để kiểm seller
            var detail = await _db.orderdetails
                                  .AsNoTracking()
                                  .SingleOrDefaultAsync(d => d.orderdetailid == id);
            if (detail == null || detail.sellerid != farmerId)
                return Forbid();

            // Lấy order tương ứng
            var order = await _db.orders
                                 .SingleOrDefaultAsync(o => o.orderid == detail.orderid);
            if (order == null)
                return NotFound();

            order.status = "Đã duyệt";    // hoặc "Paid"/"Pending" tuỳ ngữ nghĩa
            _db.orders.Update(order);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

    }


}
