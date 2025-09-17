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
                                    .Include(p => p.reviews)
                                    .ToListAsync();
            return View(products);
        }
        // GET /Products/Category/5
        //[HttpGet("Products/Category/{id:int}")]
        public async Task<IActionResult> Category(int id)
        {
            // Lấy list các category để build dropdown nếu cần
            ViewBag.Categories = await _db.categories.ToListAsync();
            ViewBag.CurrentCategoryId = id;

            // Lọc products theo categoryid
            var products = await _db.products
                                    .Include(p => p.productimages)
                                    .Include(p => p.reviews)
                                    .Where(p => p.categoryid == id)
                                    .ToListAsync();
            return View("Category", products);
        }
        // GET /Products/Details/5
        //[HttpGet("Products/Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.products
                .Include(p => p.productimages)
                //.Include(p => p.user.fullname)
                .Include(p => p.category)
                .Include(p => p.reviews)
                    .ThenInclude(r => r.customer)    // <-- Đảm bảo nạp đủ thông tin khách hàng
                .FirstOrDefaultAsync(p => p.productid == id);

            if (product == null) return NotFound();
            return View("Details", product);
        }

    }
}
