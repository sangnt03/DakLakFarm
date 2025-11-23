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

        // GET: /Products?page=1
        public async Task<IActionResult> Index(int page = 1)
        {
            // phân trang
            int pageSize = 10; // Đặt số lượng sản phẩm mỗi trang (bạn có thể thay đổi)
            var query = _db.products
                           .Include(p => p.productimages)
                           .Include(p => p.reviews)
                           .OrderByDescending(p => p.productid); // Luôn OrderBy trước khi Skip/Take

            int totalItems = await query.CountAsync();
            var products = await query.Skip((page - 1) * pageSize)
                                      .Take(pageSize)
                                      .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.PageNumber = page;
            //done phân trang

            ViewBag.Categories = await _db.categories.ToListAsync();
            ViewBag.CurrentCategoryId = null; // Vì đây là trang Index (All)

            return View(products);
        }

        // GET /Products/Category/5?page=1
        public async Task<IActionResult> Category(int id, int page = 1)
        {
            //phan trang
            int pageSize = 10; // Đặt số lượng sản phẩm mỗi trang
            var query = _db.products
                           .Include(p => p.productimages)
                           .Include(p => p.reviews)
                           .Where(p => p.categoryid == id)
                           .OrderByDescending(p => p.productid); // Luôn OrderBy

            int totalItems = await query.CountAsync();
            var products = await query.Skip((page - 1) * pageSize)
                                      .Take(pageSize)
                                      .ToListAsync();

            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.PageNumber = page;
            //done phân trang

            ViewBag.Categories = await _db.categories.ToListAsync();
            ViewBag.CurrentCategoryId = id; // Đánh dấu category đang được chọn

            // Trả về view "Category", bạn cần tạo file này
            return View("Category", products);
        }

        // GET /Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.products
                .Include(p => p.productimages)
                .Include(p => p.category)
                .Include(p => p.reviews)
                    .ThenInclude(r => r.customer)
                .FirstOrDefaultAsync(p => p.productid == id);

            if (product == null) return NotFound();
            var soldCount = await _db.orderdetails
                .Include(od => od.order)
                .Where(od => od.productid == id && od.order.status != "Cancelled")
                .SumAsync(od => (int?)od.quantity) ?? 0; 

            ViewBag.SoldCount = soldCount;

            return View("Details", product);
        }
    }
}