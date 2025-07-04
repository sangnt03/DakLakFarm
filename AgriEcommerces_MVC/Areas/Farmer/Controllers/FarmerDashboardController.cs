using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Areas.Farmer.Services;
using AgriEcommerces_MVC.Areas.Farmer.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class FarmerDashboardController : Controller
    {
        private readonly IOrderService _orderService;
        public FarmerDashboardController(IOrderService orderService)
            => _orderService = orderService;

        public async Task<IActionResult> Index(int? year, int? month)
        {
            int y = year ?? DateTime.Now.Year;
            int farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var stats = await _orderService.GetRevenueAsync(farmerId, y, month);

            ViewBag.Year = y;
            ViewBag.Month = month;
            return View(stats);
        }
    }
}
