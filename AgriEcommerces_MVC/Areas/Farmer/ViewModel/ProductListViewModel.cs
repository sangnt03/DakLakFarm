using System.Collections.Generic;
using AgriEcommerces_MVC.Models;

namespace AgriEcommerces_MVC.Areas.Farmer.ViewModels
{
    public class ProductListViewModel
    {
        public IEnumerable<product> Products { get; set; } = new List<product>();
        public string? SearchString { get; set; }
        public int? CategoryId { get; set; }
        public IEnumerable<category> Categories { get; set; } = new List<category>();
    }
}
