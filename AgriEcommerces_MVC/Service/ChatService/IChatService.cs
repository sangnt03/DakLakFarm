using AgriEcommerces_MVC.Models;
namespace AgriEcommerces_MVC.Service.ChatService
{
    public interface IChatService
    {
        Task<Message> SaveMessageAsync(string senderId, string receiverId, string content, int? productId);
        Task<IEnumerable<Message>> GetConversationHistoryAsync(string currentUserId, string targetUserId, int skip, int take);
    }
}
