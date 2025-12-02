using AgriEcommerces_MVC.Service.ChatService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    // Phương thức gửi tin nhắn
    public async Task SendMessageToUser(string receiverId, string content, int? productId)
    {
        // Lấy ID người gửi từ CustomUserIdProvider (được đọc từ Cookie)
        string senderId = Context.UserIdentifier!;

        if (string.IsNullOrEmpty(senderId))
        {
            throw new HubException("Người dùng chưa được xác thực.");
        }

        // 1. LƯU TIN NHẮN VÀO POSTGRESQL
        var message = await _chatService.SaveMessageAsync(senderId, receiverId, content, productId);

        // 2. CHUẨN BỊ DỮ LIỆU GỬI REAL-TIME
        var messageData = new
        {
            senderId = senderId,
            receiverId = receiverId,
            content = content,
            productId = productId,
            timestamp = message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // 3. GỬI TIN NHẮN CHỈ ĐẾN 2 NGƯỜI LIÊN QUAN
        // Gửi đến người nhận
        await Clients.User(receiverId).SendAsync("ReceiveMessage", messageData);

        // Gửi về chính người gửi (để cập nhật UI)
        await Clients.Caller.SendAsync("ReceiveMessage", messageData);
    }
}