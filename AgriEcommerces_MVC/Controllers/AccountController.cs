using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
            
            // Thay vì so sánh chuỗi trực tiếp, dùng Bcrypt.Verify
            if (!BCrypt.Net.BCrypt.Verify(vm.password, user.passwordhash))
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
                // Dùng Bcrypt.HashPassword để băm mật khẩu
                passwordhash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
                role = "Customer"
            };

            _db.users.Add(user);
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Login", "Account");
        }

        // GET:ChangePassword
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }
        // POST: ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1) Lấy userId từ claim
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Challenge(); // Chưa đăng nhập
            }
            int userId = int.Parse(userIdClaim.Value);

            // 2) Lấy user từ DB
            var userInDb = await _db.users.FirstOrDefaultAsync(u => u.userid == userId);
            if (userInDb == null)
            {
                return NotFound();
            }
            // 3) Xác thực mật khẩu cũ bằng BCrypt.Verify
            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, userInDb.passwordhash))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
                return View(model);
            }
            
            // 4) Băm và lưu mật khẩu mới (dùng BCrypt.HashPassword) vào DB
            userInDb.passwordhash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            try
            {
                _db.users.Update(userInDb);
                await _db.SaveChangesAsync();
                TempData["SuccessChangePassword"] = "Đổi mật khẩu thành công!";
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Lỗi khi đổi mật khẩu cho userId={UserId}", userId); // Giữ nguyên nếu bạn có inject _logger
                TempData["ErrorChangePassword"] = "Đã có lỗi xảy ra khi lưu mật khẩu mới. Vui lòng thử lại.";
                return RedirectToAction(nameof(ChangePassword));
            }

            return RedirectToAction(nameof(ChangePassword));
        }



        // HÀM (1): Bắt đầu quá trình đăng nhập Google
        // Hàm này được gọi khi người dùng nhấp nút "Đăng nhập với Google"
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            // Yêu cầu redirect đến nhà cung cấp bên ngoài (Google)
            // chỉ định Google sẽ gọi lại hàm "ExternalLoginCallback" sau khi xong
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl
            };

            // "Google" là tên scheme bạn đã đặt trong .AddGoogle() ở Program.cs
            return new ChallengeResult(provider, properties);
        }

        // HÀM (2): Google gọi lại hàm này sau khi xác thực thành công
        // Middleware /signin-google sẽ gọi hàm này
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/"); // Mặc định về trang chủ

            if (remoteError != null)
            {
                ModelState.AddModelError("", $"Lỗi từ Google: {remoteError}");
                return View(nameof(Login)); // Quay lại trang Login nếu có lỗi
            }

            // Lấy thông tin từ cookie "External" mà middleware đã tạo
            var info = await HttpContext.AuthenticateAsync("External");
            if (info == null || !info.Succeeded)
            {
                ModelState.AddModelError("", "Lỗi khi lấy thông tin đăng nhập từ Google.");
                return View(nameof(Login));
            }

            // Lấy các thông tin (claims) mà Google trả về
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            var providerKey = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier); // ID của Google

            if (email == null)
            {
                ModelState.AddModelError("", "Không thể lấy thông tin Email từ Google.");
                return View(nameof(Login));
            }

            // 1. Kiểm tra xem email này đã tồn tại trong DB chưa
            var user = await _db.users.FirstOrDefaultAsync(u => u.email == email);

            // 2. Nếu user CHƯA tồn tại -> Tự động tạo user mới
            if (user == null)
            {
                user = new user
                {
                    fullname = name,
                    email = email,
                    // Chúng ta tạo một mật khẩu ngẫu nhiên đã băm
                    // vì user này sẽ luôn đăng nhập qua Google
                    passwordhash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    role = "Customer"
                    // Thêm các trường bắt buộc khác (như phonenumber) nếu DB của bạn yêu cầu
                };
                _db.users.Add(user);
                await _db.SaveChangesAsync();
            }

            // 3. (Giống hệt hàm Login) Tạo cookie "Customer" cho user
            var claims = new List<Claim>
            {
            new Claim(ClaimTypes.NameIdentifier, user.userid.ToString()),
            new Claim(ClaimTypes.Name, user.fullname ?? user.email),
            new Claim(ClaimTypes.Email, user.email),
            new Claim(ClaimTypes.Role, user.role)
            };

            var identity = new ClaimsIdentity(claims, "Customer"); // Quan trọng: đúng tên scheme "Customer"
            var principal = new ClaimsPrincipal(identity);

            // Đăng nhập user vào scheme "Customer"
            await HttpContext.SignInAsync("Customer", principal);

            // Xóa cookie "External" tạm thời
            await HttpContext.SignOutAsync("External");

            // 4. Chuyển hướng user về trang trước đó hoặc trang chủ
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            else
                return RedirectToAction("Index", "Home");
        }

    // KẾT THÚC PHẦN CODE MỚI

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