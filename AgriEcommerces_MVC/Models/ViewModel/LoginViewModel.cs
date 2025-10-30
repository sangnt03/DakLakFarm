using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [DataType(DataType.Password)]
        public string password { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
