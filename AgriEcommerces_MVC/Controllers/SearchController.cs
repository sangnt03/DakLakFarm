using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgriEcommerces_MVC.Data;     
using AgriEcommerces_MVC.Models;

namespace AgriEcommerces_MVC.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _db;
        public SearchController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return RedirectToAction("Index", "Products");

            var pattern = $"%{q.Trim()}%";
            var results = await _db.products
                .Where(p =>
                    EF.Functions.ILike(p.productname, pattern) ||
                    EF.Functions.ILike(p.description ?? "", pattern))
                .Include(p => p.productimages)
                .ToListAsync();

            ViewBag.Query = q;
            return View(results);
        }

    }
}
