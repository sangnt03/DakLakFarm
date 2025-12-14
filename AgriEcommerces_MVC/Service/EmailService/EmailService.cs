using AgriEcommerces_MVC.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgriEcommerces_MVC.Service.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

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

            var payload = new
            {
                sender = new { email = senderEmail, name = senderName },
                to = new[] { new { email = toEmail, name = toName } },
                subject = subject,
                htmlContent = htmlContent
            };

            var client = _httpClientFactory.CreateClient();
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            try
            {
                var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", jsonContent);
                if (!response.IsSuccessStatusCode)
                {
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
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px; }}
        .header {{ background: #28a745; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #ffffff; padding: 20px; }}
        .order-info {{ background: #f8f9fa; padding: 15px; margin: 15px 0; border-left: 5px solid #28a745; border-radius: 4px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ padding: 12px; border-bottom: 1px solid #ddd; font-size: 14px; }}
        th {{ background-color: #f1f1f1; text-align: left; font-weight: bold; }}
        .text-right {{ text-align: right; }}
        .text-center {{ text-align: center; }}
        .total-row td {{ font-weight: bold; font-size: 16px; color: #28a745; border-top: 2px solid #28a745; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; border-top: 1px solid #eee; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin:0;'>🎉 Đặt hàng thành công!</h1>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{order.customername}</strong>,</p>
            <p>Cảm ơn bạn đã đặt hàng tại <strong>DakLakFarm</strong>.</p>
            
            <div class='order-info'>
                <h3 style='margin-top:0;'>📋 Thông tin đơn hàng</h3>
                <p style='margin: 5px 0;'><strong>Mã đơn hàng:</strong> <span style='font-family: monospace; font-size: 16px; color: #d63384; font-weight:bold;'>#{order.ordercode}</span></p>
                <p style='margin: 5px 0;'><strong>Ngày đặt:</strong> {order.orderdate?.ToString("dd/MM/yyyy HH:mm")}</p>
                <p style='margin: 5px 0;'><strong>Số điện thoại:</strong> {order.customerphone}</p>
                <p style='margin: 5px 0;'><strong>Địa chỉ giao hàng:</strong> {order.shippingaddress}</p>
            </div>

            <h3>🛒 Chi tiết sản phẩm</h3>
            <table>
                <thead>
                    <tr>
                        <th style='width: 40%;'>Sản phẩm</th>
                        <th class='text-center' style='width: 15%;'>SL</th>
                        <th class='text-right' style='width: 20%;'>Đơn giá</th>
                        <th class='text-right' style='width: 25%;'>Thành tiền</th>
                    </tr>
                </thead>
                <tbody>");

            if (order.orderdetails != null)
            {
                foreach (var item in order.orderdetails)
                {
                    // Dùng SumPrice có sẵn trong Model
                    sb.Append($@"
                    <tr>
                        <td>{item.product?.productname ?? "Sản phẩm nông sản"}</td>
                        <td class='text-center'>{item.quantity}</td>
                        <td class='text-right'>{item.unitprice:N0} đ</td>
                        <td class='text-right'>{item.SumPrice:N0} đ</td>
                    </tr>");
                }
            }

            sb.Append("</tbody>");
            sb.Append("<tfoot>");

            // 1. Tạm tính (Subtotal)
            sb.Append($@"
                    <tr>
                        <td colspan='3' class='text-right'><strong>Tạm tính:</strong></td>
                        <td class='text-right'>{order.totalamount:N0} đ</td>
                    </tr>");

            // 2. Phí vận chuyển (ShippingFee) - Mới thêm
            if (order.ShippingFee > 0)
            {
                sb.Append($@"
                    <tr>
                        <td colspan='3' class='text-right'><strong>Phí vận chuyển:</strong></td>
                        <td class='text-right'>{order.ShippingFee:N0} đ</td>
                    </tr>");
            }
            else
            {
                sb.Append($@"
                    <tr>
                        <td colspan='3' class='text-right'><strong>Phí vận chuyển:</strong></td>
                        <td class='text-right'>Miễn phí</td>
                    </tr>");
            }

            // 3. Giảm giá (Discount)
            if (order.discountamount > 0)
            {
                sb.Append($@"
                    <tr style='color: #dc3545;'>
                        <td colspan='3' class='text-right'><strong>Giảm giá ({order.PromotionCode}):</strong></td>
                        <td class='text-right'>-{order.discountamount:N0} đ</td>
                    </tr>");
            }

            // 4. Tổng thanh toán (FinalAmount)
            sb.Append($@"
                    <tr class='total-row'>
                        <td colspan='3' class='text-right'>TỔNG CỘNG:</td>
                        <td class='text-right'>{order.FinalAmount:N0} đ</td>
                    </tr>
                </tfoot>
            </table>

            <div style='background: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0; font-size: 14px;'>
                <p style='margin: 0;'><strong>ℹ️ Lưu ý:</strong></p>
                <ul style='margin: 5px 0 0 20px; padding: 0;'>
                    <li>Đơn hàng sẽ được xử lý trong vòng 1-2 ngày làm việc.</li>
                    <li>Vui lòng giữ điện thoại để nhân viên giao hàng liên hệ.</li>
                </ul>
            </div>
            <p>Trân trọng,<br><strong>Đội ngũ DakLakFarm</strong></p>
        </div>
        <div class='footer'>
            <p>&copy; 2025 DakLakFarm. All rights reserved.</p>
        </div>
    </div>
</body>
</html>");
            return sb.ToString();
        }



        // 3. EMAIL FARMER
        private string GenerateFarmerNotificationHtml(order order, List<orderdetail> farmerProducts)
        {
            var sb = new StringBuilder();

            decimal totalRealIncome = farmerProducts.Sum(p => p.FarmerRevenue);

            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px; }}
        .header {{ background: #0d6efd; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #ffffff; padding: 20px; }}
        .order-info {{ background: #f0f8ff; padding: 15px; margin: 15px 0; border-left: 5px solid #0d6efd; border-radius: 4px; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ padding: 10px; text-align: left; border-bottom: 1px solid #ddd; font-size: 14px; }}
        th {{ background: #f1f1f1; }}
        .text-right {{ text-align: right; }}
        .text-center {{ text-align: center; }}
        .highlight {{ color: #198754; font-weight: bold; }} /* Màu xanh lá cho tiền thực nhận */
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin:0;'>🔔 Bạn có đơn hàng mới!</h1>
        </div>
        
        <div class='content'>
            <p>Xin chào Nhà cung cấp,</p>
            <p>Sản phẩm của bạn vừa được đặt mua trong đơn hàng <strong>#{order.ordercode}</strong>. Vui lòng chuẩn bị hàng ngay.</p>
            
            <div class='order-info'>
                <h3 style='margin-top:0;'>📋 Thông tin giao hàng</h3>
                <p style='margin: 5px 0;'><strong>Khách hàng:</strong> {order.customername}</p>
                <p style='margin: 5px 0;'><strong>SĐT:</strong> {order.customerphone}</p>
                <p style='margin: 5px 0;'><strong>Địa chỉ:</strong> {order.shippingaddress}</p>
                <p style='margin: 5px 0;'><strong>Ngày đặt:</strong> {order.orderdate?.ToString("dd/MM/yyyy HH:mm")}</p>
            </div>

            <h3>📦 Chi tiết doanh thu</h3>
            <p><em>(Số liệu dưới đây đã trừ chiết khấu sàn, đây là số tiền bạn thực nhận vào ví)</em></p>
            <table>
                <thead>
                    <tr>
                        <th style='width: 35%;'>Sản phẩm</th>
                        <th class='text-center' style='width: 15%;'>SL</th>
                        <th class='text-right' style='width: 25%;'>Giá bán lẻ</th>
                        <th class='text-right' style='width: 25%;'>Thực nhận</th>
                    </tr>
                </thead>
                <tbody>");

            foreach (var item in farmerProducts)
            {
               
                sb.Append($@"
                    <tr>
                        <td>{item.product?.productname ?? "Sản phẩm"}</td>
                        <td class='text-center'>{item.quantity}</td>
                        <td class='text-right' style='color: #6c757d; text-decoration: line-through; font-size: 12px;'>
                            {item.SumPrice:N0} đ
                        </td>
                        <td class='text-right highlight'>
                            {item.FarmerRevenue:N0} đ
                        </td>
                    </tr>");
            }

            sb.Append($@"
                </tbody>
                <tfoot>
                    <tr style='background: #e9ecef;'>
                        <td colspan='3' class='text-right'><strong>TỔNG THỰC NHẬN:</strong></td>
                        <td class='text-right' style='font-size: 16px; color: #198754; font-weight: bold;'>
                            {totalRealIncome:N0} đ
                        </td>
                    </tr>
                </tfoot>
            </table>

            <div style='background: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0; border: 1px solid #ffecb5;'>
                <p style='margin: 0; color: #856404;'><strong>⚠️ Yêu cầu hành động:</strong></p>
                <ul style='margin: 5px 0 0 20px; padding: 0; color: #856404;'>
                    <li>Vui lòng xác nhận và chuẩn bị hàng trong vòng 24h.</li>
                    <li>Đảm bảo đóng gói đúng quy cách nông sản.</li>
                </ul>
            </div>

            <center>
                <a href='https://daklakfarm.onrender.com/Farmer/FarmerAccount/Login' style='display: inline-block; padding: 10px 20px; background: #0d6efd; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>Truy cập trang quản lý</a>
            </center>
            
            <p style='margin-top: 20px;'>Trân trọng,<br><strong>Ban quản trị DakLakFarm</strong></p>
        </div>
    </div>
</body>
</html>");

            return sb.ToString();
        }

        private string GeneratePasswordResetOtpHtml(string email, string otpCode)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; color: #333; }}
        .container {{ max-width: 500px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px; }}
        .otp {{ font-size: 32px; font-weight: bold; color: #007bff; letter-spacing: 5px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>🔑 Đặt lại Mật khẩu</h2>
        <p>Xin chào <strong>{email}</strong>,</p>
        <p>Mã OTP xác thực của bạn là:</p>
        <div class='otp'>{otpCode}</div>
        <p>Mã có hiệu lực trong 5 phút. Vui lòng không chia sẻ mã này.</p>
        <p>&copy; 2025 DakLakFarm</p>
    </div>
</body>
</html>";
        }
    }
}