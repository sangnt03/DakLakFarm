using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Họ và tên không được để trống.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        // ... các trường Password và ConfirmPassword của bạn đã có ErrorMessage tiếng Việt (rất tốt) ...
        [Required]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "{0} phải có ít nhất {2} ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Xác nhận mật khẩu")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; }
    }
}