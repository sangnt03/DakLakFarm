using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;
using AgriEcommerces_MVC.Helpers;

namespace AgriEcommerces_MVC.Service.ChatService
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _db;

        public ChatService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<Message> SaveMessageAsync(string senderId, string receiverId, string content, int? productId)
        {
            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                ProductId = productId,
                Timestamp = DateTimeHelper.GetVietnamTime()
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();
            return message;
        }

        public async Task<IEnumerable<Message>> GetConversationHistoryAsync(string currentUserId, string targetUserId, int skip, int take)
        {
            // Lấy tin nhắn giữa hai người dùng (người A gửi B HOẶC người B gửi A)
            return await _db.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == targetUserId) ||
                            (m.SenderId == targetUserId && m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
    }
}