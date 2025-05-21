using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Areas.Farmer.ApiModels
{
    public class ProductDto
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string ProductName { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public int QuantityAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> ImageUrls { get; set; }
        public List<int> ImageIds { get; set; }
    }
}