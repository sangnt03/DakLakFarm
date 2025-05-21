using Microsoft.AspNetCore.Mvc;

namespace AgriEcommerces_MVC.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }

        public decimal Total => UnitPrice * Quantity;
    }
}
