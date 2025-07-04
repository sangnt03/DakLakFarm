using System;
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
    public class ManagerDashboard : Controller
    {
        private readonly ApplicationDbContext _db;

        public ManagerDashboard(ApplicationDbContext db)
        {
            _db = db;
        }
        public async Task<IActionResult> Index()
        {
            return View();
        }
    }
}
