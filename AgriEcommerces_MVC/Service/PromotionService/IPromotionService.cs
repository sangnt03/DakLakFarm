using AgriEcommerces_MVC.Models; // Namespace của Models
using AgriEcommerces_MVC.Models.ViewModel; // Namespace của CartViewModel

public interface IPromotionService
{
    Task<PromotionValidationResult> ValidatePromotionAsync(string code, CartViewModel cart, int customerId, string customerType);
}

// Lớp kết quả trả về
public class PromotionValidationResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }
    public promotion? Promotion { get; set; }
    public decimal DiscountAmount { get; set; }
}