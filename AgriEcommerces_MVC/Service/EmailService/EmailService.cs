using AgriEcommerces_MVC.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json; // Thư viện xử lý JSON có sẵn của .NET

namespace AgriEcommerces_MVC.Service.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        // Inject thêm IHttpClientFactory
        public EmailService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendOrderConfirmationEmailAsync(order order, string customerEmail)
        {
            string subject = $"Thông tin đơn hàng #{order.ordercode} - DakLakFarm";
            string htmlContent = GenerateOrderConfirmationHtml(order);
            await SendEmailViaBrevoApi(customerEmail, order.customername, subject, htmlContent);
        }

        public async Task SendOrderNotificationToFarmerAsync(order order, string farmerEmail, List<orderdetail> farmerProducts)
        {
            string subject = $"Thông báo đơn hàng mới #{order.ordercode}";
            string htmlContent = GenerateFarmerNotificationHtml(order, farmerProducts);
            await SendEmailViaBrevoApi(farmerEmail, "Người bán", subject, htmlContent);
        }

        public async Task SendPasswordResetOtpAsync(string email, string otpCode)
        {
            string subject = "Mã OTP đặt lại mật khẩu - DakLakFarm";
            string htmlContent = GeneratePasswordResetOtpHtml(email, otpCode);
            await SendEmailViaBrevoApi(email, email, subject, htmlContent);
        }

        // ==============================================================================
        // CORE: HÀM GỬI MAIL QUA API (KHÔNG DÙNG SMTP) - CHẠY 100% TRÊN RENDER FREE
        // ==============================================================================
        private async Task SendEmailViaBrevoApi(string toEmail, string toName, string subject, string htmlContent)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var apiKey = emailSettings["BrevoApiKey"];
            var senderEmail = emailSettings["SenderEmail"] ?? "no-reply@daklakfarm.com";
            var senderName = emailSettings["SenderName"] ?? "DakLakFarm";

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Chưa cấu hình BrevoApiKey trong Environment Variables");
            }

            // 1. Tạo Payload JSON theo chuẩn của Brevo API
            var payload = new
            {
                sender = new { email = senderEmail, name = senderName },
                to = new[]
                {
                    new { email = toEmail, name = toName }
                },
                subject = subject,
                htmlContent = htmlContent
            };

            // 2. Chuẩn bị Request HTTP
            var client = _httpClientFactory.CreateClient();
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            // Thêm Header API Key
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            try
            {
                Console.WriteLine($"[Email API] Đang gửi mail tới {toEmail} qua Brevo...");

                // 3. Gọi API (Cổng 443 - Không bao giờ bị chặn)
                var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Email API] Gửi thành công!");
                }
                else
                {
                    // Đọc lỗi trả về nếu thất bại
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Email API Error]: {response.StatusCode} - {error}");
                    throw new Exception($"Lỗi gửi mail API: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Exception]: {ex.Message}");
                throw;
            }
        }





        private string GenerateOrderConfirmationHtml(order order)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .order-info {{ background: white; padding: 15px; margin: 15px 0; border-left: 4px solid #28a745; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background: #f5f5f5; }}
        .total {{ font-size: 18px; font-weight: bold; color: #28a745; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Đặt hàng thành công!</h1>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{order.customername}</strong>,</p>
            <p>Cảm ơn bạn đã đặt hàng tại <strong>DakLakFarm</strong>. Đơn hàng của bạn đã được tiếp nhận và đang chờ xử lý.</p>
            
            <div class='order-info'>
                <h3>📋 Thông tin đơn hàng</h3>
                <p><strong>Mã đơn hàng:</strong> <span style='font-family: monospace; font-size: 18px; color: #667eea;'>{order.ordercode}</span></p>
                <p><strong>Ngày đặt:</strong> {order.orderdate?.ToString("dd/MM/yyyy HH:mm")}</p>
                <p><strong>Địa chỉ giao hàng:</strong> {order.shippingaddress}</p>
                <p><strong>Số điện thoại:</strong> {order.customerphone}</p>
            </div>

            <h3>🛒 Chi tiết đơn hàng</h3>
            <table>
                <thead>
                    <tr>
                        <th>Sản phẩm</th>
                        <th>Số lượng</th>
                        <th>Đơn giá</th
                        <th>Thành tiền</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var item in order.orderdetails)
            {
                sb.Append($@"
                    <tr>
                        <td>{item.product?.productname ?? "Sản phẩm"}</td>
                        <td>{item.quantity}</td>
                        <td>{item.unitprice:N0} VNĐ</td>
                        <td>{(item.quantity * item.unitprice):N0} VNĐ</td>
                    </tr>");
            }

            sb.Append($@"
                    <tr>
                        <td colspan='3' style='text-align: right;'><strong>Tạm tính:</strong></td>
                        <td>{order.totalamount:N0} VNĐ</td>
                    </tr>
            ");
            sb.Append($@"
                    
                    <tr>
                        <td colspan='3' style='text-align: right;'><strong>Tạm tính:</strong></td>
                        <td>{{order.totalamount:N0}} VNĐ</td>
                    </tr>
            ");
            if (order.discountamount > 0)
            {
                sb.Append($@"
                    <tr style='color: #28a745;'>
                        <td colspan='3' style='text-align: right;'><strong>Giảm giá ({order.PromotionCode}):</strong></td>
                        <td>-{order.discountamount:N0} VNĐ</td>
                    </tr>");
            }

            sb.Append($@"
                    <tr style='background: #f0f8f0;'>
                        <td colspan='3' style='text-align: right;'><strong>Tổng cộng:</strong></td>
                        <td class='total'>{order.FinalAmount:N0} VNĐ</td>
                    </tr>
                </tfoot>
            </table>

            <div style='background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='margin: 0;'><strong>ℹ️ Lưu ý:</strong></p>
                <ul style='margin: 10px 0;'>
                    <li>Đơn hàng sẽ được xử lý trong vòng 1-2 ngày làm việc</li>
                    <li>Bạn có thể theo dõi trạng thái đơn hàng trong tài khoản của mình</li>
                    <li>Nếu có thắc mắc, vui lòng liên hệ hotline: 0999999999</li>
                </ul>
            </div>

            <p>Trân trọng,<br><strong>Đội ngũ DakLakFarm</strong></p>
        </div>

        <div class='footer'>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
            <p>&copy; 2025 DakLakFarm. All rights reserved.</p>
        </div>
    </div>
</body>
</html>");

            return sb.ToString();
        }

        private string GenerateFarmerNotificationHtml(order order, List<orderdetail> farmerProducts)
        {
            var sb = new StringBuilder();
            decimal farmerTotal = farmerProducts.Sum(p => p.quantity * p.unitprice);

            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #2196F3; color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .order-info {{ background: white; padding: 15px; margin: 15px 0; border-left: 4px solid #2196F3; }}
        table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background: #f5f5f5; }}
        .action-btn {{ display: inline-block; padding: 10px 20px; background: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔔 Đơn hàng mới</h1>
        </div>
        
        <div class='content'>
            <p>Xin chào,</p>
            <p>Bạn có một đơn hàng mới cần xử lý:</p>
            
            <div class='order-info'>
                <h3>📋 Thông tin đơn hàng</h3>
                <p><strong>Mã đơn hàng:</strong> <span style='font-family: monospace; font-size: 18px; color: #2196F3;'>{order.ordercode}</span></p>
                <p><strong>Ngày đặt:</strong> {order.orderdate?.ToString("dd/MM/yyyy HH:mm")}</p>
                <p><strong>Khách hàng:</strong> {order.customername}</p>
                <p><strong>Số điện thoại:</strong> {order.customerphone}</p>
                <p><strong>Địa chỉ giao hàng:</strong> {order.shippingaddress}</p>
            </div>

            <h3>📦 Sản phẩm của bạn trong đơn hàng</h3>
            <table>
                <thead>
                    <tr>
                        <th>Sản phẩm</th>
                        <th>Số lượng</th>
                        <th>Đơn giá</th>
                        <th>Thành tiền</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var item in farmerProducts)
            {
                sb.Append($@"
                    <tr>
                        <td>{item.product?.productname ?? "Sản phẩm"}</td>
                        <td>{item.quantity}</td>
                        <td>{item.unitprice:N0} VNĐ</td>
                        <td>{(item.quantity * item.unitprice):N0} VNĐ</td>
                    </tr>");
            }

            sb.Append($@"
                </tbody>
                <tfoot>
                    <tr style='background: #e3f2fd;'>
                        <td colspan='3' style='text-align: right;'><strong>Tổng doanh thu:</strong></td>
                        <td><strong>{farmerTotal:N0} VNĐ</strong></td>
                    </tr>
                </tfoot>
            </table>

            <div style='background: #fff3e0; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='margin: 0;'><strong>⚠️ Vui lòng:</strong></p>
                <ul style='margin: 10px 0;'>
                    <li>Chuẩn bị sản phẩm trong vòng 24h</li>
                    <li>Cập nhật trạng thái đơn hàng kịp thời</li>
                    <li>Liên hệ khách hàng nếu có vấn đề</li>
                </ul>
            </div>

            <center>
                <a href='#' class='action-btn'>Xem chi tiết đơn hàng</a>
            </center>

            <p>Trân trọng,<br><strong>Hệ thống DakLakFarm</strong></p>
        </div>
    </div>
</body>
</html>");

            return sb.ToString();
        }

        private string GeneratePasswordResetOtpHtml(string email, string otpCode)
        {
            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .otp-code {{ 
            font-size: 28px; 
            font-weight: bold; 
            color: #007bff; 
            text-align: center; 
            margin: 20px 0; 
            letter-spacing: 5px;
            padding: 15px;
            background: #e7f3ff;
            border-radius: 5px;
            font-family: monospace;
        }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔑 Yêu cầu Đặt lại Mật khẩu</h1>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{email}</strong>,</p>
            <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn tại <strong>DakLakFarm</strong>.</p>
            <p>Mã OTP của bạn là:</p>
            
            <div class='otp-code'>
                {otpCode}
            </div>
            
            <p>Mã này sẽ hết hạn trong 5 phút. Vui lòng không chia sẻ mã này với bất kỳ ai.</p>
            <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
            
            <p>Trân trọng,<br><strong>Đội ngũ DakLakFarm</strong></p>
        </div>

        <div class='footer'>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
            <p>&copy; 2025 DakLakFarm. All rights reserved.</p>
        </div>
    </div>
</body>
</html>");
            return sb.ToString();
        }
    }
}