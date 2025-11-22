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

            // Lấy detail để xác thực Farmer sở hữu đơn này
            var detail = await _db.orderdetails
                                  .AsNoTracking()
                                  .SingleOrDefaultAsync(d => d.orderdetailid == id);

            if (detail == null || detail.sellerid != farmerId)
                return Forbid();

            var order = await _db.orders
                                 .SingleOrDefaultAsync(o => o.orderid == detail.orderid);

            if (order == null)
                return NotFound();

            // --- LOGIC XỬ LÝ TRẠNG THÁI ĐA NGÔN NGỮ ---

            // Nhóm 1: Cần duyệt (COD mới đặt hoặc Chờ duyệt)
            string[] pendingStatuses = { "Chờ duyệt", "Pending" };

            // Nhóm 2: Đã duyệt hoặc Đã thanh toán (Sẵn sàng giao hàng)
            string[] readyToShipStatuses = { "Đã duyệt", "Approved", "Paid" };

            if (pendingStatuses.Contains(order.status))
            {
                // Chuyển từ Chờ duyệt -> Đã duyệt
                order.status = "Đã duyệt";
                TempData["Success"] = "Đơn hàng đã được duyệt thành công! Vui lòng chuẩn bị hàng.";
            }
            else if (readyToShipStatuses.Contains(order.status))
            {
                // Chuyển từ Đã duyệt/Paid -> Đang vận chuyển
                order.status = "Đang vận chuyển"; // Hoặc "Shipped"
                TempData["Success"] = "Đơn hàng đã chuyển sang trạng thái đang vận chuyển.";
            }
            else
            {
                TempData["Error"] = $"Không thể thay đổi trạng thái. Trạng thái hiện tại: {order.status}";
                return RedirectToAction("Index");
            }

            _db.orders.Update(order);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}