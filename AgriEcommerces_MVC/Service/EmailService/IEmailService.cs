using AgriEcommerces_MVC.Models;

namespace AgriEcommerces_MVC.Service.EmailService
{
    public interface IEmailService
    {
        Task SendOrderConfirmationEmailAsync(order order, string customerEmail);
        Task SendOrderNotificationToFarmerAsync(order order, string farmerEmail, List<orderdetail> farmerProducts);
    }
}