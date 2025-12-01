namespace AgriEcommerces_MVC.Models.ViewModel
{
    public class ChatViewModel
    {
        // ID của người nhận (Farmer/Admin)
        public string ReceiverId { get; set; } = null!;

        // Tên của cửa hàng/người nhận
        public string ReceiverName { get; set; } = null!;

        // ID của sản phẩm đang được hỏi
        public int ProductId { get; set; }

        // Tên sản phẩm
        public string ProductName { get; set; } = null!;
    }
}