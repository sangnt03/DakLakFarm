using System.Security.Claims;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Route("api/farmer/categories")]
    [ApiController]
    [Authorize(Roles = "Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class CategoriesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public CategoriesApiController(ApplicationDbContext db) => _db = db;

        // GET api/farmer/categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAll()
        {
            var list = await _db.categories
                .Select(c => new {
                    CategoryId = c.categoryid,
                    CategoryName = c.categoryname
                })
                .ToListAsync();
            return Ok(list);
        }
    }
}
