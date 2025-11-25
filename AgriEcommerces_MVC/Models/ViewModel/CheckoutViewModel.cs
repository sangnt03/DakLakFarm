using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class CheckoutViewModel
    {
        public CartViewModel Cart { get; set; }
        public int? SellerId { get; set; }

        // Danh sách địa chỉ đã lưu của khách hàng
        public List<customer_address> SavedAddresses { get; set; } = new List<customer_address>();

        // ID của địa chỉ được chọn
        [Required(ErrorMessage = "Vui lòng chọn một địa chỉ giao hàng.")]
        public int? SelectedAddressId { get; set; }

        // ---- Phần Khuyến mãi ----
        public string? PromotionCode { get; set; } // Mã được áp dụng
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; } // Tổng tiền cuối
        public bool IsPromotionApplied { get; set; }

        public bool IsBuyNow { get; set; }
        public string PaymentMethod { get; set; } = "COD"; // Mặc định là COD

    }
}