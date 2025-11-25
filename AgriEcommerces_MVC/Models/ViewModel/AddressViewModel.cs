using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class AddressViewModel
    {
        public int Id { get; set; } // Dùng cho việc Edit

        [Display(Name = "Họ tên người nhận")]
        [Required(ErrorMessage = "Vui lòng nhập tên người nhận")]
        [StringLength(255)]
        public string RecipientName { get; set; }

        [Display(Name = "Số điện thoại")]
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(20)]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Địa chỉ chi tiết")]
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ chi tiết (số nhà, tên đường)")]
        [StringLength(500)]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và có 10 chữ số)")]
        public string FullAddress { get; set; }

        [Display(Name = "Tỉnh/Thành phố")]
        [Required(ErrorMessage = "Vui lòng chọn Tỉnh/Thành phố")]
        [StringLength(100)]
        public string ProvinceCity { get; set; }

        [Display(Name = "Quận/Huyện")]
        [Required(ErrorMessage = "Vui lòng chọn Quận/Huyện")]
        [StringLength(100)]
        public string District { get; set; }

        [Display(Name = "Phường/Xã")]
        [Required(ErrorMessage = "Vui lòng chọn Phường/Xã")]
        [StringLength(100)]
        public string WardCommune { get; set; }

        [Display(Name = "Đặt làm địa chỉ mặc định")]
        public bool IsDefault { get; set; }
    }
}