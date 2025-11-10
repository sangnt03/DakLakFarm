using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BCrypt.Net; // Đảm bảo đã cài đặt package BCrypt.Net-Next

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Route("Management/[controller]/[action]")]
    // Controller này sử dụng Authentication Scheme riêng là "ManagerAuth"
    // Chỉ cho phép role "Admin" truy cập các action yêu cầu xác thực
    [Authorize(AuthenticationSchemes = "ManagerAuth", Roles = "Admin")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AccountController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: Login
        [HttpGet]
        [AllowAnonymous] // Cho phép truy cập không cần đăng nhập
        public IActionResult Login(string? returnUrl = null)
        {
            // Nếu người dùng đã đăng nhập với scheme ManagerAuth, chuyển hướng ngay
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "ManagerDashboard", new { area = "Management" });
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Kiểm tra tính hợp lệ của model (vd: email, password không được để trống)
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Tìm user theo email
            var user = await _db.users.FirstOrDefaultAsync(u => u.email == model.email);

            // Kiểm tra user tồn tại
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không chính xác.");
                return View(model);
            }

            // Kiểm tra mật khẩu bằng BCrypt.Verify(password_nhập_vào, password_hash_trong_db)
            // Giả định property lưu hash trong db là 'passwordhash'
            bool isPasswordValid = !string.IsNullOrEmpty(user.passwordhash) &&
                                   BCrypt.Net.BCrypt.Verify(model.password, user.passwordhash);

            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không chính xác.");
                return View(model);
            }

            // Kiểm tra quyền truy cập (chỉ cho phép Admin)
            if (!string.Equals(user.role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Bạn không có quyền truy cập vào khu vực này.");
                return View(model);
            }

            // Đăng nhập thành công -> Tạo ClaimsIdentity
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.userid.ToString()),
                new Claim(ClaimTypes.Name, user.fullname ?? user.email),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Role, user.role) // Quan trọng để [Authorize(Roles="Admin")] hoạt động
            };

            var claimsIdentity = new ClaimsIdentity(claims, "ManagerAuth"); // Sử dụng đúng tên scheme

            var authProperties = new AuthenticationProperties
            {
                
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) 
            };

            // Thực hiện đăng nhập với scheme "ManagerAuth"
            await HttpContext.SignInAsync("ManagerAuth", new ClaimsPrincipal(claimsIdentity), authProperties);

            // Chuyển hướng sau khi đăng nhập thành công
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "ManagerDashboard", new { area = "Management" });
        }

        // POST: Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Đăng xuất khỏi scheme "ManagerAuth"
            await HttpContext.SignOutAsync("ManagerAuth");
            return RedirectToAction(nameof(Login), "Account", new { area = "Management" });
        }

        // GET: AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}