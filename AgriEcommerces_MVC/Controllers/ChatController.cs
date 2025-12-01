using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models.ViewModel;
using AgriEcommerces_MVC.Service.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

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

        // ==========================================
        // 1. TRANG CHÍNH (INBOX TẬP TRUNG)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(string? receiverId, int? productId)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return RedirectToAction("Login", "Account");

            // A. LẤY DANH SÁCH ĐÃ CHAT (Logic cũ của bạn)
            var conversations = await _db.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    UserId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.Timestamp).FirstOrDefault()
                })
                .ToListAsync();

            var userIds = conversations.Select(c => c.UserId).ToList();
            var users = await _db.users
                .Where(u => userIds.Contains(u.userid.ToString()))
                .Select(u => new { u.userid, u.fullname, u.shop_name })
                .ToListAsync();

            // Chuyển đổi sang List ViewModel để dễ thao tác thêm sửa xóa
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

            // B. XỬ LÝ LOGIC "CHAT NGAY" (NẾU CÓ PARAMETER)
            if (!string.IsNullOrEmpty(receiverId))
            {
                // 1. Kiểm tra xem người này đã có trong danh sách chat chưa
                var existingConv = result.FirstOrDefault(c => c.UserId == receiverId);

                if (existingConv == null)
                {
                    // 2. Nếu chưa, tìm thông tin người đó trong DB
                    if (int.TryParse(receiverId, out int rIdInt))
                    {
                        var receiverUser = await _db.users.FindAsync(rIdInt);
                        if (receiverUser != null)
                        {
                            // 3. Tạo một cuộc hội thoại "giả" đưa lên đầu danh sách
                            var newConv = new ConversationViewModel
                            {
                                UserId = receiverId,
                                UserName = receiverUser.shop_name ?? receiverUser.fullname ?? "Người dùng",
                                LastMessage = "Bắt đầu cuộc trò chuyện mới...",
                                Timestamp = DateTime.Now,
                                AvatarChar = (receiverUser.shop_name ?? receiverUser.fullname ?? "N").Substring(0, 1).ToUpper()
                            };
                            result.Insert(0, newConv);
                        }
                    }
                }
                else
                {
                    // Nếu đã có, đưa lên đầu danh sách để dễ thấy
                    result.Remove(existingConv);
                    result.Insert(0, existingConv);
                }

                // Truyền dữ liệu xuống View để JS tự động load khung chat bên phải
                ViewBag.SelectedUserId = receiverId;
                ViewBag.SelectedProductId = productId;
            }

            return View(result);
        }

        // ==========================================
        // 2. API LẤY KHUNG CHAT (BÊN PHẢI) - AJAX LOAD
        // ==========================================
        [HttpGet]
        public IActionResult GetChatBoxPartial(string receiverId, int? productId)
        {
            // Tìm tên người nhận để hiển thị trên Header khung chat
            // Lưu ý: receiverId trong DB của bạn là string hay int? Code của bạn lúc thì string lúc int
            // Tôi giả định User bảng users có userid là int, nhưng Identity User id là string.
            // Đoạn này bạn cần điều chỉnh theo đúng kiểu dữ liệu project.

            string receiverName = "Người dùng";
            if (int.TryParse(receiverId, out int idInt))
            {
                var user = _db.users.FirstOrDefault(u => u.userid == idInt);
                if (user != null) receiverName = user.shop_name ?? user.fullname;
            }

            string productName = null;
            if (productId.HasValue)
            {
                var product = _db.products.FirstOrDefault(p => p.productid == productId);
                productName = product?.productname;
            }

            var viewModel = new ChatViewModel
            {
                ReceiverId = receiverId,
                ReceiverName = receiverName,
                ProductId = productId ?? 0,
                ProductName = productName // Có thể null nếu không phải chat từ sản phẩm
            };

            // Trả về Partial View chứa nội dung khung chat (File _ChatBox.cshtml)
            // Bạn có thể tái sử dụng nội dung của _ChatModal.cshtml nhưng bỏ thẻ <div modal> bao quanh
            return PartialView("_ChatBox", viewModel);
        }

        // ==========================================
        // 3. API LẤY LỊCH SỬ CHAT (GIỮ NGUYÊN)
        // ==========================================
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