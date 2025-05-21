using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class CartViewModel
    {
        public List<CartItem> Items { get; set; } = new();
        public decimal GrandTotal => Items.Sum(i => i.Total);
    }
}