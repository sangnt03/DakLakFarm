using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string email { get; set; }

        [Required, DataType(DataType.Password)]
        public string password { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
