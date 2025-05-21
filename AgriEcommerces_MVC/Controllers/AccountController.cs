using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AccountController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var vm = new LoginViewModel { ReturnUrl = returnUrl };
            ViewData["ReturnUrl"] = returnUrl;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _db.users.FirstOrDefaultAsync(u => u.email == vm.email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email chưa đăng ký tài khoản.");
                return View(vm);
            }

            if (user.passwordhash != vm.password)
            {
                ModelState.AddModelError("", "Mật khẩu không đúng.");
                return View(vm);
            }

            // Tạo claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.userid.ToString()),
                new Claim(ClaimTypes.Name, user.fullname ?? user.email),
                new Claim(ClaimTypes.Email, user.email),
                new Claim(ClaimTypes.Role, user.role)
            };

            // Khai báo identity dùng scheme "Customer"
            var identity = new ClaimsIdentity(claims, "Customer");
            var principal = new ClaimsPrincipal(identity);

            // Sign in vào scheme Customer
            await HttpContext.SignInAsync("Customer", principal);

            // Redirect về ReturnUrl nếu hợp lệ
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            var vm = new RegisterViewModel();
            ViewData["ReturnUrl"] = returnUrl;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            bool exists = await _db.users.AnyAsync(u => u.email == vm.Email);
            if (exists)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email đã được sử dụng.");
                return View(vm);
            }

            var user = new user
            {
                fullname = vm.FullName,
                email = vm.Email,
                passwordhash = vm.Password,    // TODO: Hash trước khi lưu!
                role = "Customer"
            };

            _db.users.Add(user);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Login", "Account");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Clear session nếu cần
            HttpContext.Session.Remove("Cart");

            // Sign out khỏi scheme Customer
            await HttpContext.SignOutAsync("Customer");

            return RedirectToAction("Index", "Home");
        }
    }
}
