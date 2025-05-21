// Areas/Farmer/Controllers/FarmerAccountController.cs
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Route("Farmer/[controller]/[action]")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class FarmerAccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        public FarmerAccountController(ApplicationDbContext db) => _db = db;

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

            if (!user.role.Equals("Farmer", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Chỉ tài khoản Farmer mới được phép đăng nhập.");
                return View(vm);
            }

            // Tạo Claims và SignIn với scheme "FarmerAuth"
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.userid.ToString()),
                new Claim(ClaimTypes.Name, user.fullname ?? user.email),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Role, user.role)
            };
            var identity = new ClaimsIdentity(claims, "FarmerAuth");
            await HttpContext.SignInAsync("FarmerAuth", new ClaimsPrincipal(identity));

            // Redirect về ReturnUrl nếu hợp lệ, hoặc dashboard
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "FarmerDashboard", new { area = "Farmer" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("FarmerAuth");
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
