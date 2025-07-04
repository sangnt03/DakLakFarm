using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgriEcommerces_MVC.Areas.Farmer.ViewModels
{
    public class ProductEditViewModel
    {
        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(20)]
        public string? Unit { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityAvailable { get; set; }

        /// <summary>
        /// Danh sách ảnh mới để upload (tùy chọn khi edit)
        /// </summary>
        public List<IFormFile>? ProductImages { get; set; }
    }
}