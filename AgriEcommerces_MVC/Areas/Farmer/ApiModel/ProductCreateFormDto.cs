// Areas/Farmer/ApiModels/ProductCreateFormDto.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriEcommerces_MVC.Areas.Farmer.ApiModels
{
    public class ProductCreateFormDto
    {
        [Required]
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Unit { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityAvailable { get; set; }

        /// <summary>
        /// Tập hợp ảnh sản phẩm; sẽ lưu vào bảng productimage
        /// </summary>
        public List<IFormFile>? ProductImages { get; set; }
    }
}
