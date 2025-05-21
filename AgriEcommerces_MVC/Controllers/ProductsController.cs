using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ProductsController(ApplicationDbContext db) => _db = db;

        // GET: /Products
        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _db.categories.ToListAsync();
            ViewBag.CurrentCategoryId = null;
            // include luôn collection productimages để lấy hình
            var products = await _db.products
                                    .Include(p => p.productimages)
                                    .ToListAsync();
            return View(products);
        }
        // GET /Products/Category/5
        [HttpGet("Products/Category/{id:int}")]
        public async Task<IActionResult> Category(int id)
        {
            // Lấy list các category để build dropdown nếu cần
            ViewBag.Categories = await _db.categories.ToListAsync();
            ViewBag.CurrentCategoryId = id;

            // Lọc products theo categoryid
            var products = await _db.products
                                    .Include(p => p.productimages)
                                    .Where(p => p.categoryid == id)
                                    .ToListAsync();
            return View("Category", products);
        }
    }
}
