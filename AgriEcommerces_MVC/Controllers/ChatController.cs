using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using AgriEcommerces_MVC.Service.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using AgriEcommerces_MVC.Helpers;

namespace AgriEcommerces_MVC.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IChatService _chatService;

        public ChatController(ApplicationDbContext db, IChatService chatService)
        {
            _db = db;
            _chatService = chatService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? receiverId, int? productId)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return RedirectToAction("Login", "Account");

            // A. LẤY DANH SÁCH ĐÃ CHAT
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
                    UserName = user?.shop_name ?? user?.fullname ?? "Unknown",
                    LastMessage = c.LastMessage?.Content,
                    Timestamp = c.LastMessage?.Timestamp,
                    AvatarChar = (user?.shop_name ?? user?.fullname ?? "U").Substring(0, 1).ToUpper()
                };
            }).OrderByDescending(c => c.Timestamp).ToList();

            // Xử lý receiverId từ tham số (Chat Ngay)
            if (!string.IsNullOrEmpty(receiverId))
            {
                var existingConv = result.FirstOrDefault(c => c.UserId == receiverId);

                if (existingConv == null)
                {
                    if (int.TryParse(receiverId, out int rIdInt))
                    {
                        var receiverUser = await _db.users.FindAsync(rIdInt);
                        if (receiverUser != null)
                        {
                            var newConv = new ConversationViewModel
                            {
                                UserId = receiverId,
                                UserName = receiverUser.shop_name ?? receiverUser.fullname ?? "Người dùng",
                                LastMessage = "Bắt đầu cuộc trò chuyện mới...",
                                Timestamp = DateTimeHelper.GetVietnamTime(),
                                AvatarChar = (receiverUser.shop_name ?? receiverUser.fullname ?? "N").Substring(0, 1).ToUpper()
                            };
                            result.Insert(0, newConv);
                        }
                    }
                }
                else
                {
                    result.Remove(existingConv);
                    result.Insert(0, existingConv);
                }

                ViewBag.SelectedUserId = receiverId;
                ViewBag.SelectedProductId = productId;
            }

            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetChatBoxPartial(string receiverId, int? productId)
        {
            string receiverName = "Người dùng";
            if (int.TryParse(receiverId, out int idInt))
            {
                // Thêm await và ToListAsync hoặc FirstOrDefaultAsync
                var user = await _db.users.FirstOrDefaultAsync(u => u.userid == idInt);
                if (user != null) receiverName = user.shop_name ?? user.fullname;
            }

            string productName = null;
            if (productId.HasValue)
            {
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

            return PartialView("_ChatBox", viewModel);
        }


        [HttpGet]
        public async Task<IActionResult> GetChatHistory(string targetUserId, int skip = 0, int take = 20)
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

    // Helper Class để thay thế dynamic (Giúp code dễ đọc & tránh lỗi runtime)
    public class ConversationViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime? Timestamp { get; set; }
        public string AvatarChar { get; set; }
    }
}