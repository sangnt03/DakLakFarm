using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReviewController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claimValue, out var id) ? id : 0;
    }

    // GET: /Review/Create?productid=34
    [HttpGet]
    public async Task<IActionResult> Create(int productid)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return RedirectToAction("Login", "Account");

        var hasDeliveredOrder = await _db.orders
            .Where(o => o.customerid == userId && o.status == "Đã nhận hàng")
            .AnyAsync(o => o.orderdetails.Any(od => od.productid == productid));

        if (!hasDeliveredOrder)
        {
            TempData["Error"] = "Bạn chỉ có thể đánh giá sản phẩm đã nhận được.";
            return RedirectToAction("Details", "Products", new { id = productid });
        }

        // Kiểm tra xem đã đánh giá chưa
        var existingReview = await _db.reviews
            .FirstOrDefaultAsync(r => r.productid == productid && r.customerid == userId);

        if (existingReview != null)
        {
            
            return RedirectToAction("Details", "Products", new { id = productid });
        }

        var model = new review
        {
            productid = productid,
            customerid = userId,
            rating = 0,
            comment = null,
            createdat = null
        };
        return View(model);
    }


    // POST: /Review/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(review review)
    {
        var userId = GetCurrentUserId();
        if (userId == 0)
            return RedirectToAction("Login", "Account");

        // 1. Kiểm tra xem user có phải đã nhận hàng hay chưa
        var hasDeliveredOrder = await _db.orders
            .Where(o => o.customerid == userId && o.status == "Đã nhận hàng")
            .AnyAsync(o => o.orderdetails.Any(od => od.productid == review.productid));

        if (!hasDeliveredOrder)
        {
            TempData["Error"] = "Bạn không có quyền đánh giá sản phẩm này.";
            return RedirectToAction("Details", "Products", new { id = review.productid });
        }

        // 2. Kiểm tra xem user đã đánh giá sản phẩm này chưa
        var existingReview = await _db.reviews
            .FirstOrDefaultAsync(r => r.productid == review.productid && r.customerid == userId);
        if (existingReview != null)
        {
            
            return RedirectToAction("Details", "Products", new { id = review.productid });
        }

        // 3. Validate rating và comment
        if (review.rating < 1 || review.rating > 5)
        {
            TempData["Error"] = "Điểm đánh giá phải từ 1 đến 5.";
            return RedirectToAction("Create", new { productid = review.productid });
        }

        if (!string.IsNullOrEmpty(review.comment) && review.comment.Length > 500)
        {
            TempData["Error"] = "Bình luận không được vượt quá 500 ký tự.";
            return RedirectToAction("Create", new { productid = review.productid });
        }

        // 4. Lưu review vào DB
        review.customerid = userId;
        review.createdat = DateTime.Now;
        _db.reviews.Add(review);
        await _db.SaveChangesAsync();

        // 5. Tạo URL để redirect về trang chi tiết sản phẩm
        var redirectUrl = Url.Action("Details", "Products", new { id = review.productid });

        TempData["Success"] = "Đánh giá của bạn đã được gửi thành công!";
        return RedirectToAction("Details", "Products", new { area = "", id = review.productid });

    }

}