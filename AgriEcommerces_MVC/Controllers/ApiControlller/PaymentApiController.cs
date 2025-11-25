using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Service.VnPayService;
using AgriEcommerces_MVC.Service.EmailService; // THÊM DÒNG NÀY
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Controllers.ApiController
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly VNPayService _vnPayService;
        private readonly IEmailService _emailService; // THÊM DÒNG NÀY
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            VNPayService vnPayService,
            IEmailService emailService,     // THÊM DÒNG NÀY
            IConfiguration configuration,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _vnPayService = vnPayService;
            _emailService = emailService;   // THÊM DÒNG NÀY
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("VNPayIPN")]
        public async Task<IActionResult> VNPayIPN()
        {
            var queryParams = Request.Query;

            try
            {
                // Xác thực chữ ký
                var vnp_SecureHash = queryParams["vnp_SecureHash"];
                var hashSecret = _configuration["VNPay:HashSecret"];
                if (!_vnPayService.ValidateSignature(queryParams, vnp_SecureHash, hashSecret))
                {
                    _logger.LogWarning("Invalid VNPay signature");
                    return Ok(new { RspCode = "97", Message = "Invalid signature" });
                }

                // Lấy thông tin giao dịch
                var orderId = int.Parse(queryParams["vnp_TxnRef"]);
                var responseCode = queryParams["vnp_ResponseCode"];
                var transactionNo = queryParams["vnp_TransactionNo"];
                var amount = decimal.Parse(queryParams["vnp_Amount"]) / 100;

                _logger.LogInformation($"VNPay IPN received for Order {orderId}, ResponseCode: {responseCode}");

                // Lấy đơn hàng + Include customer để dùng email
                var order = await _context.orders
                    .Include(o => o.customer)           // THÊM DÒNG NÀY – QUAN TRỌNG!
                    .Include(o => o.orderdetails)
                    .FirstOrDefaultAsync(o => o.orderid == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    return Ok(new { RspCode = "01", Message = "Order not found" });
                }

                if (amount != order.FinalAmount)
                {
                    _logger.LogWarning($"Amount mismatch for Order {orderId}");
                    return Ok(new { RspCode = "04", Message = "Amount invalid" });
                }

                if (await _context.Payments.AnyAsync(p => p.OrderId == orderId && p.Status == "Success"))
                {
                    _logger.LogInformation($"Order {orderId} already paid");
                    return Ok(new { RspCode = "02", Message = "Order already confirmed" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    if (responseCode == "00") // Thành công
                    {
                        // Cập nhật Payment
                        var payment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Pending");

                        if (payment != null)
                        {
                            payment.Status = "Success";
                            payment.GatewayTransactionCode = transactionNo;
                        }

                        // Cập nhật Order
                        order.status = "Paid";

                        // Cập nhật Promotion
                        if (order.promotionid.HasValue)
                        {
                            var promotion = await _context.promotions.FindAsync(order.promotionid.Value);
                            if (promotion != null)
                            {
                                promotion.CurrentUsageCount++;
                                _context.promotion_usagehistories.Add(new promotion_usagehistory
                                {
                                    PromotionId = order.promotionid.Value,
                                    UserId = order.customerid,
                                    OrderId = orderId,
                                    UsedAt = DateTime.SpecifyKind(DateTimeHelper.GetVietnamTime(), DateTimeKind.Unspecified),
                                    DiscountAmount = order.discountamount
                                });
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        _logger.LogInformation($"Order {orderId} paid successfully. Wallet & promotion updated.");

                        // GỬI EMAIL THẬT – AN TOÀN 100%, KHÔNG CÒN LỖI DISPOSED
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Gửi cho khách hàng
                                if (!string.IsNullOrEmpty(order.customer?.email))
                                {
                                    await _emailService.SendOrderConfirmationEmailAsync(order, order.customer.email);
                                    _logger.LogInformation($"Email xác nhận đã gửi tới khách hàng: {order.customer.email}");
                                }

                                // Gửi cho từng Farmer
                                var farmerGroups = order.orderdetails.GroupBy(od => od.sellerid);
                                foreach (var group in farmerGroups)
                                {
                                    var farmer = await _context.users
                                        .FirstOrDefaultAsync(u => u.userid == group.Key && u.role == "Farmer");

                                    if (farmer != null && !string.IsNullOrEmpty(farmer.email))
                                    {
                                        await _emailService.SendOrderNotificationToFarmerAsync(order, farmer.email, group.ToList());
                                        _logger.LogInformation($"Email thông báo đã gửi tới Farmer: {farmer.email}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Lỗi gửi email cho đơn hàng {orderId}");
                            }
                        });

                        return Ok(new { RspCode = "00", Message = "Confirm Success" });
                    }
                    else // Thất bại
                    {
                        var payment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Pending");

                        if (payment != null)
                        {
                            payment.Status = "Failed";
                            payment.GatewayTransactionCode = transactionNo;
                        }

                        order.status = "Cancelled";
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Ok(new { RspCode = "00", Message = "Confirm Success" });
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error processing payment for Order {orderId}");
                    return Ok(new { RspCode = "99", Message = "Unknown error" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VNPay IPN Error");
                return Ok(new { RspCode = "99", Message = "Unknown error" });
            }
        }

        [HttpPost("MoMoIPN")]
        public async Task<IActionResult> MoMoIPN()
        {
            try
            {
                // 1. Đọc dữ liệu từ MoMo gửi về
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(body);

                string resultCode = json.resultCode;
                string orderIdRaw = json.orderId; // Chuỗi dạng "94_6389..."
                string transId = json.transId;    // Mã giao dịch MoMo

                int orderId;
                if (orderIdRaw.Contains("_"))
                {
                    string[] parts = orderIdRaw.Split('_');
                    if (!int.TryParse(parts[0], out orderId))
                    {
                        _logger.LogError($"Lỗi format OrderId MoMo: {orderIdRaw}");
                        return StatusCode(400); // Bad Request
                    }
                }
                else
                {
                    // Trường hợp dự phòng nếu MoMo gửi đúng số
                    if (!int.TryParse(orderIdRaw, out orderId))
                    {
                        return StatusCode(400);
                    }
                }

                var order = await _context.orders
                    .Include(o => o.customer)      // Lấy thông tin khách hàng
                    .Include(o => o.orderdetails)  // Lấy chi tiết đơn để gửi mail cho Farmer
                    .FirstOrDefaultAsync(o => o.orderid == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Không tìm thấy Order {orderId} từ IPN MoMo");
                    return Ok(new { message = "Order not found" });
                }

                // 2. Xử lý kết quả giao dịch
                if (resultCode == "0") // Thành công (0 là mã chuẩn của MoMo)
                {
                    if (order.status != "Paid")
                    {
                        // Cập nhật trạng thái đơn hàng
                        order.status = "Paid";

                        // Cập nhật bảng Payment
                        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
                        if (payment != null)
                        {
                            payment.Status = "Success";
                            payment.GatewayTransactionCode = transId;
                        }

                        // --- [BỔ SUNG] Logic cập nhật Promotion (Giống VNPay) ---
                        if (order.promotionid.HasValue)
                        {
                            var promotion = await _context.promotions.FindAsync(order.promotionid.Value);
                            if (promotion != null)
                            {
                                promotion.CurrentUsageCount++;
                                _context.promotion_usagehistories.Add(new promotion_usagehistory
                                {
                                    PromotionId = order.promotionid.Value,
                                    UserId = order.customerid,
                                    OrderId = orderId,
                                    UsedAt = DateTime.SpecifyKind(DateTimeHelper.GetVietnamTime(), DateTimeKind.Unspecified),
                                    DiscountAmount = order.discountamount
                                });
                            }
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Thanh toán MoMo thành công cho Order {orderId}");

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (order.customer != null && !string.IsNullOrEmpty(order.customer.email))
                                {
                                    await _emailService.SendOrderConfirmationEmailAsync(order, order.customer.email);
                                    _logger.LogInformation($"Email xác nhận đã gửi tới khách hàng: {order.customer.email}");
                                }

                                if (order.orderdetails != null)
                                {
                                    var farmerGroups = order.orderdetails.GroupBy(od => od.sellerid);
                                    foreach (var group in farmerGroups)
                                    {
                                        try
                                        {
                                            var farmer = await _context.users
                                                .FirstOrDefaultAsync(u => u.userid == group.Key && u.role == "Farmer");

                                            if (farmer != null && !string.IsNullOrEmpty(farmer.email))
                                            {
                                                await _emailService.SendOrderNotificationToFarmerAsync(order, farmer.email, group.ToList());
                                            }
                                        }
                                        catch (Exception exInner)
                                        {
                                            _logger.LogError($"Lỗi lấy info Farmer trong Task: {exInner.Message}");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Lỗi tổng quá trình gửi email background cho đơn {orderId}");
                            }
                        });
                    }
                }
                else
                {
                    // Giao dịch thất bại
                    var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
                    if (payment != null)
                    {
                        payment.Status = "Failed";
                        payment.GatewayTransactionCode = transId;
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Thanh toán MoMo thất bại/hủy bỏ cho Order {orderId}");
                }

                return StatusCode(204);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoMo IPN Error System");
                return StatusCode(204);
            }
        }
    }
}