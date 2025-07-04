using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Authorize(Roles = "Admin")]
    public class ManagerSellerController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ManagerSellerController(ApplicationDbContext db) => _db = db;

        // GET: /Management/ManagerSeller/Index
        public async Task<IActionResult> Index()
        {
            var list = await _db.sellerrequests
                .Include(r => r.User)
                .Where(r => r.Status == "Chưa duyệt")
                .OrderBy(r => r.RequestDate)
                .ToListAsync();
            return View(list);
        }

        // POST: /Management/ManagerSeller/Approve
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int requestId)
        {
            var req = await _db.sellerrequests.FindAsync(requestId);
            if (req == null) return NotFound();

            // Chỉ gán Status, EF sẽ tự nhận diện property thay đổi
            req.Status = "Đã duyệt";  // hoặc "Approved"

            // Cập nhật role trong bảng user
            var usr = await _db.users.FindAsync(req.UserId);
            if (usr != null)
            {
                usr.role = "Farmer";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Management/ManagerSeller/Reject
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int requestId)
        {
            var req = await _db.sellerrequests.FindAsync(requestId);
            if (req == null) return NotFound();

            req.Status = "Đã từ chối";  // hoặc "Rejected"

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
