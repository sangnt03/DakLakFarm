using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Utilities
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        // Đọc giá trị từ ClaimTypes.NameIdentifier ở AccountController đã set
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
