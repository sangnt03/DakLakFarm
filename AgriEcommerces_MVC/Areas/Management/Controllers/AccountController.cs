using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Route("Management/[controller]/[action]")]
    [Authorize(AuthenticationSchemes = "ManagerAuth", Roles = "Admin")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AccountController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            var vm = new LoginViewModel { ReturnUrl = returnUrl };
            ViewData["ReturnUrl"] = returnUrl;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _db.users
                .FirstOrDefaultAsync(u => u.email == vm.email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email chưa được đăng ký.");
                return View(vm);
            }

            // Nếu bạn đang lưu hash, thay thế bằng VerifyPasswordHash(...)
            if (user.passwordhash != vm.password)
            {
                ModelState.AddModelError("", "Mật khẩu không đúng.");
                return View(vm);
            }

            if (!user.role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Chỉ tài khoản quản lý mới được phép đăng nhập.");
                return View(vm);
            }

            // Tạo Claims và SignIn với scheme "ManagerAuth"
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.userid.ToString()),
                new Claim(ClaimTypes.Name, user.fullname ?? user.email),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Role, user.role)
            };
            var identity = new ClaimsIdentity(claims, "ManagerAuth");
            await HttpContext.SignInAsync("ManagerAuth", new ClaimsPrincipal(identity));

            // Redirect về ReturnUrl nếu hợp lệ, hoặc dashboard
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "ManagerDashboard", new { area = "Management" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(AuthenticationSchemes = "ManagerAuth", Roles = "Admin")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("ManagerAuth");
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
