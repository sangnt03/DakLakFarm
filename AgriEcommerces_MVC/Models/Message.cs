
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models;
    public class Message
    {
        // Id sẽ là khóa chính tự tăng (do EF Core và Serial trong Postgres xử lý)
        public int Id { get; set; }

        // Dùng string để nhất quán với ClaimTypes.NameIdentifier (user.userid.ToString())
        public string SenderId { get; set; } = null!;

        public string ReceiverId { get; set; } = null!;

        public string Content { get; set; } = null!;

        public int? ProductId { get; set; } // Có thể null

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;
    }