using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(Roles = "Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class ProductManagementController : Controller
    {
        // Chỉ để trả trang chính, tất cả CRUD sẽ qua AJAX/JSON
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
