using AgriEcommerces_MVC.Controllers;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using AgriEcommerces_MVC.Service.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth")] 
    public class ChatShopController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IChatService _chatService;

        public ChatShopController(ApplicationDbContext db, IChatService chatService)
        {
            _db = db;
            _chatService = chatService;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return RedirectToAction("Login", "FarmerAccount");

            // Lấy tất cả tin nhắn liên quan
            var allMessages = await _db.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .ToListAsync();

            // Group theo người chat (không phân biệt sender/receiver)
            var conversations = allMessages
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).FirstOrDefault()
                })
                .ToList();

            var userIds = conversations.Select(c => c.UserId).Distinct().ToList();

            // Lấy thông tin KHÁCH HÀNG (người chat với Farmer)
            var users = await _db.users
                .Where(u => userIds.Contains(u.userid.ToString()))
                .Select(u => new { u.userid, u.fullname, u.shop_name })
                .ToListAsync();

            var result = conversations.Select(c =>
            {
                var user = users.FirstOrDefault(u => u.userid.ToString() == c.UserId);
                return new ConversationViewModel
                {
                    UserId = c.UserId,
                    UserName = user?.fullname ?? user?.shop_name ?? "Khách hàng",
                    LastMessage = c.LastMessage?.Content,
                    Timestamp = c.LastMessage?.Timestamp,
                    AvatarChar = (user?.fullname ?? "C").Substring(0, 1).ToUpper()
                };
            }).OrderByDescending(c => c.Timestamp).ToList();

            return View(result);
        }

        // 2. LOAD KHUNG CHAT (PARTIAL)
        [HttpGet]
        public async Task<IActionResult> GetChatBoxPartial(string receiverId, int? productId)
        {
            // Tìm tên Khách hàng để hiển thị trên header khung chat
            string receiverName = "Khách hàng";
            if (int.TryParse(receiverId, out int idInt))
            {
                // Dùng FirstOrDefaultAsync thay vì FirstOrDefault
                var user = await _db.users.FirstOrDefaultAsync(u => u.userid == idInt);
                if (user != null) receiverName = user.fullname ?? user.shop_name;
            }

            string productName = null;
            if (productId.HasValue && productId > 0)
            {
                // Dùng FirstOrDefaultAsync
                var product = await _db.products.FirstOrDefaultAsync(p => p.productid == productId);
                productName = product?.productname;
            }

            var viewModel = new ChatViewModel
            {
                ReceiverId = receiverId,
                ReceiverName = receiverName,
                ProductId = productId ?? 0,
                ProductName = productName
            };

            // Trả về View nằm trong Area Farmer
            return PartialView("_ChatBoxShop", viewModel);
        }

        // 3. API LẤY LỊCH SỬ (Gọi từ JS)
        [HttpGet]
        public async Task<IActionResult> GetChatHistory(string targetUserId, int skip = 0, int take = 50)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var messages = await _chatService.GetConversationHistoryAsync(
                currentUserId, targetUserId, skip, take
            );

            var messageList = messages.Reverse().Select(m => new
            {
                senderId = m.SenderId,
                receiverId = m.ReceiverId,
                content = m.Content,
                productId = m.ProductId,
                timestamp = m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
            });

            return Json(new { success = true, messages = messageList });
        }
        

    }
}