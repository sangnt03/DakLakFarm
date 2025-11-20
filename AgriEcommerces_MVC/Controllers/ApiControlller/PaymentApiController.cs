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

        /// <summary>
        /// IPN Webhook từ VNPay (Server-to-Server)
        /// QUAN TRỌNG: Đây là nơi xác nhận thanh toán thực sự!
        /// </summary>
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

                        // Ghi nhận doanh thu Farmer
                        foreach (var detail in order.orderdetails)
                        {
                            _context.WalletTransaction.Add(new WalletTransaction
                            {
                                FarmerId = detail.sellerid,
                                Amount = detail.FarmerRevenue,
                                Type = "OrderRevenue",
                                ReferenceId = detail.orderdetailid,
                                Description = $"Doanh thu từ đơn hàng {order.ordercode}",
                                CreateDate = DateTime.SpecifyKind(DateTimeHelper.GetVietnamTime(), DateTimeKind.Unspecified)
                            });
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
    }
}