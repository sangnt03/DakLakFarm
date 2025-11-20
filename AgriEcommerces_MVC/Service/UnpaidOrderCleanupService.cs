using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Service
{
    public class UnpaidOrderCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnpaidOrderCleanupService> _logger;

        // Múi giờ Việt Nam (UTC+7)
        private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public UnpaidOrderCleanupService(IServiceProvider serviceProvider, ILogger<UnpaidOrderCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Unpaid Order Cleanup Service đang khởi động...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupUnpaidOrdersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi quét đơn hàng chưa thanh toán.");
                }

                // Chờ 1 phút trước khi quét lại lần nữa
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CleanupUnpaidOrdersAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. Lấy thời gian hiện tại theo giờ VN
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

                // 2. Đơn hàng được coi là quá hạn nếu tạo trước mốc này (5 phút trước)
                var expirationTime = now.AddMinutes(-5);

                // 3. Tìm các đơn hàng thỏa mãn:
                // - Trạng thái là "Pending" (Chờ thanh toán)
                // - Ngày tạo < thời gian hết hạn
                // - Load kèm bảng Payments để kiểm tra phương thức
                var expiredOrders = await context.orders
                    .Include(o => o.Payments)
                    .Where(o => o.status == "Pending" && o.orderdate < expirationTime)
                    .ToListAsync();

                // 4. LỌC QUAN TRỌNG: Giữ lại đơn COD, chỉ hủy đơn Online (VNPay, MoMo...)
                // Logic: Nếu đơn hàng có bất kỳ Payment nào là "COD" -> Bỏ qua (không hủy)
                //        Nếu không có Payment hoặc PaymentMethod != "COD" -> Hủy
                var ordersToCancel = expiredOrders
                    .Where(o => !o.Payments.Any(p => p.PaymentMethod == "COD"))
                    .ToList();

                if (ordersToCancel.Any())
                {
                    _logger.LogInformation($"Tìm thấy {ordersToCancel.Count} đơn hàng treo quá hạn 5 phút. Đang tiến hành hủy...");

                    foreach (var order in ordersToCancel)
                    {
                        // --- CẬP NHẬT TRẠNG THÁI ORDER ---
                        order.status = "Đã hủy";

                        // --- TẠO LỊCH SỬ HỦY (Bắt buộc do bạn có bảng order_cancellation) ---
                        var cancellation = new order_cancellation
                        {
                            OrderId = order.orderid,

                            // Vì service chạy ngầm, không có User login. 
                            // Ta gán CancelledBy = CustomerId (coi như khách hàng bỏ đơn)
                            // Hoặc bạn có thể set cứng ID của Admin (ví dụ: 1)
                            CancelledBy = order.customerid,

                            CancelReason = "Hủy tự động do quá hạn thanh toán (5 phút)",
                            CancelledAt = now,
                            RefundAmount = 0, // Chưa thanh toán nên hoàn tiền = 0
                            RefundStatus = "N/A"
                        };

                        context.order_cancellations.Add(cancellation);

                        // --- CẬP NHẬT TRẠNG THÁI PAYMENT (Nếu có) ---
                        // Nếu đã có bản ghi payment (ví dụ VNPay Pending), chuyển sang Failed
                        foreach (var pay in order.Payments)
                        {
                            if (pay.Status == "Pending")
                            {
                                pay.Status = "Đã Hủy"; // Hoặc "Failed"
                            }
                        }

                        // Hoàn lại số lượt dùng mã giảm giá (nếu có)
                        if (order.promotionid.HasValue)
                        {
                            var promotion = await context.promotions.FindAsync(order.promotionid.Value);
                            if (promotion != null)
                            {
                                promotion.CurrentUsageCount--;
                                // Xóa lịch sử dùng mã
                                var usageHistory = await context.promotion_usagehistories
                                    .FirstOrDefaultAsync(h => h.OrderId == order.orderid);
                                if (usageHistory != null)
                                {
                                    context.promotion_usagehistories.Remove(usageHistory);
                                }
                            }
                        }
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation($"Đã hủy tự động {ordersToCancel.Count} đơn hàng.");
                }
            }
        }
    }
}