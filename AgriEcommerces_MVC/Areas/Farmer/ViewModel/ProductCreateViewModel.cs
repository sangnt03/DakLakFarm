using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriEcommerces_MVC.Areas.Farmer.ViewModels
{
    public class ProductCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Tên sản phẩm")]
        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Đơn vị tính")]
        public string? Unit { get; set; }

        [Required, Range(0.01, double.MaxValue)]
        [Display(Name = "Giá")]
        public decimal Price { get; set; }

        [Required, Range(0, int.MaxValue)]
        [Display(Name = "Số lượng")]
        public int QuantityAvailable { get; set; }

        [Display(Name = "Hình ảnh sản phẩm")]
        public List<IFormFile>? ProductImages { get; set; }
    }
}
