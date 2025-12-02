using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Utilities
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Lấy userId từ Claims (NameIdentifier)
            var userId = connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Nếu không tìm thấy, thử tìm trong các claim dự phòng (sub, userid)
            if (string.IsNullOrEmpty(userId))
            {
                userId = connection.User?.FindFirst("sub")?.Value
                      ?? connection.User?.FindFirst("userid")?.Value;
            }

            return userId;
        }
    }
}