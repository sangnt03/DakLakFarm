using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class PaymentViewModel
    {
        [BindNever]
        [ValidateNever]
        public CartViewModel Cart { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tỉnh/thành")]
        public string province { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn huyện/quận")]
        public string district { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn xã/phường")]
        public string ward { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể")]
        public string address { get; set; }

        public string customername { get; set; }
        public string customerphone { get; set; }
    }
}
