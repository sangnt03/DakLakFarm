using System.Linq;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Authorize(Roles = "Admin")]
    public class ManagerCategoryController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ManagerCategoryController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Management/ManagerCategories
        public async Task<IActionResult> Index()
        {
            var categories = await _db.categories.OrderBy(c => c.categoryid).ToListAsync();
            return View(categories);
        }

        // GET: /Management/ManagerCategories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Management/ManagerCategories/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("categoryname")] category category)
        {
            if (!ModelState.IsValid)
                return View(category);

            _db.categories.Add(category);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Management/ManagerCategories/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _db.categories.FindAsync(id);
            if (category == null)
                return NotFound();

            return View(category);
        }

        // POST: /Management/ManagerCategories/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("categoryid,categoryname")] category category)
        {
            if (id != category.categoryid)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(category);

            var existing = await _db.categories.FindAsync(id);
            if (existing == null)
                return NotFound();

            existing.categoryname = category.categoryname;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Management/ManagerCategories/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.categories.FindAsync(id);
            if (category != null)
            {
                _db.categories.Remove(category);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
