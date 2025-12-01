// Lấy các ID quan trọng từ View (ví dụ: trong thẻ input hidden)
const currentUserId = document.getElementById("currentUserId").value;
const receiverId = document.getElementById("receiverId").value;
const messageInput = document.getElementById("messageInput");

// 1. Xây dựng kết nối
const chatConnection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .withAutomaticReconnect() // Rất khuyến khích cho tính ổn định
    .build();

// 2. Đăng ký hàm nhận tin nhắn từ Server (Phải khớp với "ReceiveMessage" trong Hub)
chatConnection.on("ReceiveMessage", function (data) {
    const messagesList = document.getElementById("messagesList");
    const li = document.createElement("li");

    let contentHtml = data.content;

    if (data.productId) {
        contentHtml += ` <a href="/product/details/${data.productId}">[SP ID: ${data.productId}]</a>`;
    }

    li.innerHTML = `<strong>${data.senderId == currentUserId ? 'Bạn' : data.senderId}</strong>: ${contentHtml} <small>(${data.timestamp})</small>`;
    messagesList.appendChild(li);
    messagesList.scrollTop = messagesList.scrollHeight; // Cuộn xuống dưới
});

// 3. Bắt đầu kết nối
chatConnection.start()
    .then(() => {
        console.log("Kết nối SignalR thành công!");
        document.getElementById("sendButton").disabled = false;
    })
    .catch(err => {
        console.error("Lỗi kết nối: ", err.toString());
        // Hiển thị thông báo lỗi cho người dùng
    });

// 4. Xử lý Gửi Tin nhắn (Gắn vào nút Gửi)
document.getElementById("sendButton").addEventListener("click", function (event) {
    const message = messageInput.value.trim();
    // Lấy Product ID (nếu đang ở trang chi tiết sản phẩm, nếu không thì null)
    const productIdElement = document.getElementById("currentProductIdHidden");
    const productId = productIdElement ? (parseInt(productIdElement.value) || null) : null;

    if (message) {
        // Gọi phương thức SendMessageToUser trên Server
        chatConnection.invoke("SendMessageToUser", receiverId, message, productId)
            .then(() => {
                messageInput.value = ""; // Xóa input sau khi gửi
            })
            .catch(err => console.error("Lỗi gửi tin: ", err.toString()));
    }
    event.preventDefault();
});