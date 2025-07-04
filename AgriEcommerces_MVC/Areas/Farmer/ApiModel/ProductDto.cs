// Areas/Farmer/ApiModels/ProductDto.cs
using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Areas.Farmer.ApiModels
{
    public class ProductDto
    {
        public int ProductId { get; set; }

        /// <summary>
        /// Id của user tạo sản phẩm (nếu cần hiển thị/kiểm tra)
        /// </summary>
        public int UserId { get; set; }

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string ProductName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Unit { get; set; }

        public decimal Price { get; set; }

        public int QuantityAvailable { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// URLs của các ảnh đã upload
        /// </summary>
        public List<string> ImageUrls { get; set; } = new List<string>();

        /// <summary>
        /// ImageId tương ứng trong bảng productimage
        /// </summary>
        public List<int> ImageIds { get; set; } = new List<int>();
    }
}
