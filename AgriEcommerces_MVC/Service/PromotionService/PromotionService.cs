using AgriEcommerces_MVC.Data; // Namespace của DbContext
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.EntityFrameworkCore;

public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _context;

    public PromotionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PromotionValidationResult> ValidatePromotionAsync(string code, CartViewModel cart, int customerId, string customerType)
    {
        var result = new PromotionValidationResult { IsSuccess = false };
        if (string.IsNullOrWhiteSpace(code))
        {
            result.ErrorMessage = "Vui lòng nhập mã khuyến mãi.";
            return result;
        }

        var promo = await _context.promotions
            .FirstOrDefaultAsync(p => p.Code.ToUpper() == code.ToUpper());

        // 1. Kiểm tra tồn tại
        if (promo == null)
        {
            result.ErrorMessage = "Mã khuyến mãi không tồn tại.";
            return result;
        }

        // 2. Kiểm tra trạng thái (Active)
        if (!promo.IsActive)
        {
            result.ErrorMessage = "Mã khuyến mãi đã bị vô hiệu hóa.";
            return result;
        }

        // 3. Kiểm tra ngày hiệu lực
        var now = DateTime.UtcNow;
        if (now < promo.StartDate)
        {
            result.ErrorMessage = "Mã khuyến mãi chưa đến ngày áp dụng.";
            return result;
        }
        if (now > promo.EndDate)
        {
            result.ErrorMessage = "Mã khuyến mãi đã hết hạn.";
            return result;
        }

        // 4. Kiểm tra tổng lượt sử dụng
        if (promo.TotalUsageLimit.HasValue && promo.CurrentUsageCount >= promo.TotalUsageLimit.Value)
        {
            result.ErrorMessage = "Mã khuyến mãi đã hết lượt sử dụng.";
            return result;
        }

        // 5. Kiểm tra lượt dùng / người
        if (promo.MaxUsagePerUser.HasValue)
        {
            int userUsageCount = await _context.promotion_usagehistories
                .CountAsync(h => h.PromotionId == promo.PromotionId && h.UserId == customerId);
            if (userUsageCount >= promo.MaxUsagePerUser.Value)
            {
                result.ErrorMessage = "Bạn đã hết lượt sử dụng mã này.";
                return result;
            }
        }

        // 6. Kiểm tra giá trị đơn hàng tối thiểu
        if (promo.MinOrderValue > cart.GrandTotal)
        {
            result.ErrorMessage = $"Đơn hàng tối thiểu {promo.MinOrderValue:N0} VNĐ để áp dụng mã này.";
            return result;
        }

        // 7. Kiểm tra đối tượng khách hàng
        if (promo.TargetCustomerType != "all" && promo.TargetCustomerType != customerType)
        {
            result.ErrorMessage = "Mã không áp dụng cho loại tài khoản của bạn.";
            return result;
        }

        // 8. Tính toán giảm giá dựa trên phạm vi
        decimal eligibleTotal = 0; // Tổng tiền của các sản phẩm đủ điều kiện

        // Tải thông tin các sản phẩm trong giỏ hàng
        var productIdsInCart = cart.Items.Select(i => i.ProductId).ToList();
        var cartProductsInfo = await _context.products
            .Where(p => productIdsInCart.Contains(p.productid))
            .Select(p => new { p.productid, p.categoryid, p.userid })
            .ToListAsync();

        // Tải các phạm vi áp dụng của khuyến mãi
        var applicableProducts = (promo.ApplicableTo == "specific_products")
            ? await _context.promotion_products.Where(pp => pp.PromotionId == promo.PromotionId).Select(pp => pp.ProductId).ToListAsync()
            : new List<int>();

        var applicableCategories = (promo.ApplicableTo == "specific_categories")
            ? await _context.promotion_categories.Where(pc => pc.PromotionId == promo.PromotionId).Select(pc => pc.CategoryId).ToListAsync()
            : new List<int>();

        var applicableFarmers = (promo.ApplicableTo == "specific_farmers")
            ? await _context.promotion_farmers.Where(pf => pf.PromotionId == promo.PromotionId).Select(pf => pf.FarmerId).ToListAsync()
            : new List<int>();


        if (promo.ApplicableTo == "all")
        {
            eligibleTotal = cart.GrandTotal;
        }
        else
        {
            foreach (var item in cart.Items)
            {
                var productInfo = cartProductsInfo.FirstOrDefault(p => p.productid == item.ProductId);
                if (productInfo == null) continue;

                if (promo.ApplicableTo == "specific_products" && applicableProducts.Contains(item.ProductId))
                {
                    eligibleTotal += item.Total;
                }
                else if (promo.ApplicableTo == "specific_farmers" && applicableFarmers.Contains(productInfo.userid))
                {
                    eligibleTotal += item.Total;
                }
                else if (promo.ApplicableTo == "specific_farmers" && applicableFarmers.Contains(productInfo.userid))
                {
                    eligibleTotal += item.Total;
                }
            }
        }

        if (eligibleTotal == 0 && promo.ApplicableTo != "all")
        {
            result.ErrorMessage = "Không có sản phẩm nào trong giỏ hàng đủ điều kiện áp dụng mã này.";
            return result;
        }

        // 9. Tính số tiền giảm
        decimal discountAmount = 0;
        if (promo.DiscountType == "percentage")
        {
            discountAmount = eligibleTotal * (promo.DiscountValue / 100);
            if (promo.MaxDiscountAmount.HasValue && discountAmount > promo.MaxDiscountAmount.Value)
            {
                discountAmount = promo.MaxDiscountAmount.Value;
            }
        }
        else if (promo.DiscountType == "fixed_amount")
        {
            discountAmount = promo.DiscountValue;
            // Đảm bảo không giảm quá tổng tiền
            if (discountAmount > eligibleTotal)
            {
                discountAmount = eligibleTotal;
            }
        }
        else if (promo.DiscountType == "free_shipping")
        {
            // Tạm thời ví dụ giảm 30.000 (phí ship)
            discountAmount = 30000; // Bạn có thể thay đổi logic này
        }

        result.IsSuccess = true;
        result.DiscountAmount = discountAmount;
        result.Promotion = promo;
        return result;
    }
}