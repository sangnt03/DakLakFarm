using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriEcommerces_MVC.Controllers
{
    [Authorize(Roles = "Customer")]
    public class SellerRequestController : Controller
    {
        private readonly ApplicationDbContext _db;
        public SellerRequestController(ApplicationDbContext db) => _db = db;

        // GET: /SellerRequest/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /SellerRequest/CreateAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CreateAjax()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Kiểm xem đã có Pending chưa
            bool has = _db.sellerrequests
                          .Any(r => r.UserId == userId && r.Status == "Chưa duyệt");
            if (has)
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn đã gửi yêu cầu rồi. Vui lòng chờ quản lý duyệt."
                });
            }

            // Thêm request mới
            var req = new SellerRequest
            {
                UserId = userId,
                RequestDate = DateTime.UtcNow,
                Status = "Chưa duyệt"
            };
            _db.sellerrequests.Add(req);
            try
            {
                await _db.SaveChangesAsync();
                return Json(new
                {
                    success = true,
                    message = "Bạn đã gửi yêu cầu thành công!"
                });
            }
            catch
            {
                return Json(new
                {
                    success = false,
                    message = "Thất bại, vui lòng thử lại."
                });
            }
        }

    }
}
