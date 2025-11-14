using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Authorize(Roles = "Admin")]
    public class ManagerFarmerController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ManagerFarmerController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Management/ManagerFarmer
        public async Task<IActionResult> Index()
        {
            var farmers = await _db.users
                                   .Where(u => u.role == "Farmer")
                                   .OrderBy(u => u.userid)
                                   .ToListAsync();
            return View(farmers);
        }

        // ========= CREATE =========
        // GET: /Management/ManagerFarmer/Create
        [HttpGet]
        public IActionResult Create()
        {
            // Trả về form để Admin nhập thông tin Farmer mới
            return View();
        }

        // POST: /Management/ManagerFarmer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(user model)
        {
            // 1) Gán trước giá trị role cho model
            model.role = "Farmer";
            // 2) Xóa luôn key "role" khỏi ModelState (nếu có lỗi "required" do non-nullable)
            ModelState.Remove("role");
            // 2) Sau đó mới kiểm tra ModelState
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 3) (Nếu có xử lý hash password, v.v.)
            // Ví dụ: model.password = BCrypt.Net.BCrypt.HashPassword(model.password);

            // 4) Lưu vào DB
            _db.users.Add(model);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Thêm Farmer (ID={model.userid}) thành công.";
            return RedirectToAction(nameof(Index));
        }


        // ========= EDIT =========
        // GET: /Management/ManagerFarmer/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Tìm user có userid = id và role = "Farmer"
            var farmer = await _db.users
                                  .FirstOrDefaultAsync(u => u.userid == id && u.role == "Farmer");
            if (farmer == null)
            {
                return NotFound();
            }

            return View(farmer);
        }

        // POST: /Management/ManagerFarmer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, user model)
        {
            if (id != model.userid)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Lấy bản ghi hiện tại từ DB
            var farmerInDb = await _db.users
                                      .FirstOrDefaultAsync(u => u.userid == id && u.role == "Farmer");
            if (farmerInDb == null)
            {
                return NotFound();
            }

            // Cập nhật các trường cần thiết (không sửa role)
            farmerInDb.fullname = model.fullname;
            farmerInDb.email = model.email;
            farmerInDb.phonenumber = model.phonenumber;
            

            // Nếu có thay đổi mật khẩu, bạn có thể kiểm tra lúc form gửi lên
            // Ví dụ: nếu model.password != chuỗi rỗng thì hash và cập nhật
            if (!string.IsNullOrWhiteSpace(model.passwordhash))
            {
                // farmerInDb.password = BCrypt.Net.BCrypt.HashPassword(model.password);
                farmerInDb.passwordhash = model.passwordhash; // hoặc hash tuỳ setup của bạn
            }

            // Lưu thay đổi
            _db.users.Update(farmerInDb);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cập nhật Farmer (ID={id}) thành công.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var farmer = await _db.users
                                  .FirstOrDefaultAsync(u => u.userid == id && u.role == "Farmer");
            if (farmer == null)
                return NotFound();

            return View(farmer);
        }

        // ====== DELETE (POST) ======
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var farmer = await _db.users
                                  .FirstOrDefaultAsync(u => u.userid == id && u.role == "Farmer");
            if (farmer == null)
                return NotFound();

            _db.users.Remove(farmer);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Xóa Farmer (ID={id}) thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
