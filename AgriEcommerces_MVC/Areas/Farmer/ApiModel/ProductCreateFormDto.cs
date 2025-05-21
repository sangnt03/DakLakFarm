// Areas/Farmer/ApiModels/ProductCreateFormDto.cs
using Microsoft.AspNetCore.Http;

namespace AgriEcommerces_MVC.Areas.Farmer.ApiModels
{
    public class ProductCreateFormDto
    {
        public int CategoryId { get; set; }
        public string ProductName { get; set; } = "";
        public string? Description { get; set; }
        public string? Unit { get; set; }
        public decimal Price { get; set; }
        public int QuantityAvailable { get; set; }

        // phải là List<IFormFile> hoặc IFormFileCollection
        public List<IFormFile>? ProductImages { get; set; }
    }
}
