using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;



public class CartController : Controller
{
    private const string CART_KEY = "Cart";
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CartController> _logger;

    public CartController(ApplicationDbContext db, ILogger<CartController> logger)
    {
        _db = db;
        _logger = logger;
    }
    
    [Authorize]
    [HttpGet]
    public IActionResult Index()
    {
        var cart = HttpContext.Session.GetObject<CartViewModel>(CART_KEY)
                   ?? new CartViewModel();
        return View(cart);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AddAjax(int id, int qty = 1)
    {
        var cart = HttpContext.Session.GetObject<CartViewModel>(CART_KEY)
                   ?? new CartViewModel();

        var p = await _db.products
                         .Include(x => x.productimages)
                         .FirstOrDefaultAsync(x => x.productid == id);

        if (p != null)
        {
            // --- LOGIC KIỂM TRA TỒN KHO MỚI ---
            // Tính tổng số lượng khách muốn mua (đã có trong giỏ + muốn thêm)
            var exist = cart.Items.FirstOrDefault(i => i.ProductId == id);
            int currentInCart = exist?.Quantity ?? 0;
            int totalRequested = currentInCart + qty;

            if (totalRequested > p.quantityavailable)
            {
                // Trả về lỗi 400 (BadRequest) kèm thông báo để JavaScript hiển thị
                return BadRequest($"Kho chỉ còn {p.quantityavailable} sản phẩm. Bạn đã có {currentInCart} trong giỏ.");
            }
            // ----------------------------------

            if (exist != null)
                exist.Quantity += qty;
            else
            {
                var imageUrl = p.productimages.FirstOrDefault()?.imageurl;
                if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("~/"))
                    imageUrl = $"~{imageUrl}";

                cart.Items.Add(new CartItem
                {
                    ProductId = p.productid,
                    ProductName = p.productname,
                    UnitPrice = p.price,
                    Quantity = qty,
                    ImageUrl = imageUrl,
                    SellerId = p.userid
                });
            }
        }
        else
        {
            _logger.LogWarning($"Product not found with ID: {id}");
            return NotFound("Sản phẩm không tồn tại.");
        }

        HttpContext.Session.SetObject(CART_KEY, cart);
        return PartialView("_CartBody", cart);
    }

    [Authorize]
    [HttpGet]
    public IActionResult DecrementAjax(int id)
    {
        var cart = HttpContext.Session.GetObject<CartViewModel>(CART_KEY)
                   ?? new CartViewModel();

        var itm = cart.Items.FirstOrDefault(i => i.ProductId == id);
        if (itm != null)
        {
            itm.Quantity--;
            if (itm.Quantity <= 0) cart.Items.Remove(itm);
        }
        HttpContext.Session.SetObject(CART_KEY, cart);
        return PartialView("_CartBody", cart);
    }

    [Authorize]
    [HttpGet]
    public IActionResult UpdateAjax(int id, int qty)
    {
        var cart = HttpContext.Session.GetObject<CartViewModel>(CART_KEY)
                   ?? new CartViewModel();

        var item = cart.Items.FirstOrDefault(i => i.ProductId == id);
        if (item != null)
        {
            // --- LOGIC KIỂM TRA TỒN KHO MỚI ---
            // Lấy thông tin tồn kho thực tế từ DB
            var productInDb = _db.products.Find(id);
            if (productInDb != null)
            {
                // Nếu khách tăng số lượng (qty > 0) và số lượng đó lớn hơn kho
                if (qty > 0 && qty > productInDb.quantityavailable)
                {
                    return BadRequest($"Kho chỉ còn {productInDb.quantityavailable} sản phẩm.");
                }
            }
            // ----------------------------------

            if (qty <= 0)
                cart.Items.Remove(item);
            else
                item.Quantity = qty;
        }

        HttpContext.Session.SetObject(CART_KEY, cart);
        return PartialView("_CartBody", cart);
    }
}
