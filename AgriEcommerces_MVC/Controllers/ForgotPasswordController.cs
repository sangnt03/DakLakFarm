using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Service.EmailService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AgriEcommerces_MVC.Controllers
{
    public class ForgotPasswordController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _emailService;
        private readonly ILogger<ForgotPasswordController> _logger;

        // Múi giờ Việt Nam
        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
        }

        public ForgotPasswordController(
            ApplicationDbContext db,
            IEmailService emailService,
            ILogger<ForgotPasswordController> logger)
        {
            _db = db;
            _emailService = emailService;
            _logger = logger;
        }

        // [GET] /ForgotPassword/Index - Trang nhập email
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // [POST] /ForgotPassword/SendOtp - Gửi OTP qua email
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Vui lòng nhập địa chỉ email.";
                return RedirectToAction("Index");
            }

            // Kiểm tra email có tồn tại trong hệ thống không
            var user = await _db.users.FirstOrDefaultAsync(u => u.email.ToLower() == email.ToLower());
            if (user == null)
            {
                // Không tiết lộ email có tồn tại hay không (bảo mật)
                TempData["Success"] = "Nếu email tồn tại trong hệ thống, mã OTP đã được gửi đến email của bạn.";
                return RedirectToAction("VerifyOtp", new { email });
            }

            // Tạo mã OTP 6 chữ số
            string otpCode = GenerateOTP();

            // Lưu OTP vào database
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var passwordReset = new password_reset
            {
                Email = email.ToLower(),
                OtpCode = otpCode,
                CreatedAt = DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified),
                ExpiresAt = DateTime.SpecifyKind(vnTime.AddMinutes(10), DateTimeKind.Unspecified), // Hết hạn sau 10 phút
                IsUsed = false
            };

            _db.password_reset.Add(passwordReset);
            await _db.SaveChangesAsync();

            // Gửi email OTP
            try
            {
                await _emailService.SendPasswordResetOtpAsync(email, otpCode);
                _logger.LogInformation("OTP đã được gửi đến email {Email}", email);
                TempData["Success"] = "Mã OTP đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi OTP đến email {Email}", email);
                TempData["Error"] = "Có lỗi xảy ra khi gửi email. Vui lòng thử lại.";
                return RedirectToAction("Index");
            }

            return RedirectToAction("VerifyOtp", new { email });
        }

        // [GET] /ForgotPassword/VerifyOtp - Trang nhập OTP
        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Email = email;
            return View();
        }

        // [POST] /ForgotPassword/VerifyOtp - Xác thực OTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string email, string otpCode)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return RedirectToAction("VerifyOtp", new { email });
            }

            // Tìm OTP mới nhất chưa sử dụng
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
            var vnTimeUnspecified = DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified);

            var passwordReset = await _db.password_reset
                .Where(pr => pr.Email.ToLower() == email.ToLower()
                          && pr.OtpCode == otpCode
                          && !pr.IsUsed
                          && pr.ExpiresAt > vnTimeUnspecified)
                .OrderByDescending(pr => pr.CreatedAt)
                .FirstOrDefaultAsync();

            if (passwordReset == null)
            {
                TempData["Error"] = "Mã OTP không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("VerifyOtp", new { email });
            }

            // Đánh dấu OTP đã sử dụng
            passwordReset.IsUsed = true;
            passwordReset.UsedAt = vnTimeUnspecified;
            await _db.SaveChangesAsync();

            // Chuyển đến trang đặt lại mật khẩu
            TempData["Success"] = "Xác thực OTP thành công!";
            return RedirectToAction("ResetPassword", new { email, token = otpCode });
        }

        // [GET] /ForgotPassword/ResetPassword - Trang đặt lại mật khẩu
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        // [POST] /ForgotPassword/ResetPassword - Cập nhật mật khẩu mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                TempData["Error"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("ResetPassword", new { email, token });
            }

            if (newPassword.Length < 6)
            {
                TempData["Error"] = "Mật khẩu phải có ít nhất 6 ký tự.";
                return RedirectToAction("ResetPassword", new { email, token });
            }

            // Tìm user
            var user = await _db.users.FirstOrDefaultAsync(u => u.email.ToLower() == email.ToLower());
            if (user == null)
            {
                TempData["Error"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("Index");
            }

            // Cập nhật mật khẩu mới (hash nếu cần)
            user.passwordhash = BCrypt.Net.BCrypt.HashPassword(newPassword); // Sử dụng BCrypt để hash
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Email} đã đặt lại mật khẩu thành công", email);

            TempData["Success"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập.";
            if (user.role == "Farmer")
            {
                // Nếu là 'Farmer', chuyển hướng về trang đăng nhập của Farmer
                // URL: /Farmer/FarmerAccount/Login
                // Lưu ý: Cần chỉ rõ 'Area' nếu Controller của bạn nằm trong Area
                return RedirectToAction("Login", "FarmerAccount", new { Area = "Farmer" });
            }
            else
            {
                // Mặc định (cho 'Customer', 'Admin' hoặc vai trò khác)
                // Chuyển hướng về trang đăng nhập chính
                // URL: /Account/Login
                return RedirectToAction("Login", "Account");
            }
        }

        // [POST] /ForgotPassword/ResendOtp - Gửi lại OTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            return await SendOtp(email);
        }

        // HÀM TẠO OTP 6 CHỮ SỐ NGẪU NHIÊN
        private string GenerateOTP()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomNumber = new byte[4];
                rng.GetBytes(randomNumber);
                int value = Math.Abs(BitConverter.ToInt32(randomNumber, 0));
                return (value % 1000000).ToString("D6"); // 6 chữ số
            }
        }
    }
}