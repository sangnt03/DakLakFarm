using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Service
{
    public class UnpaidOrderCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnpaidOrderCleanupService> _logger;
        private readonly TimeZoneInfo _vietnamTimeZone;

        public UnpaidOrderCleanupService(IServiceProvider serviceProvider, ILogger<UnpaidOrderCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            
            // Xử lý đa nền tảng: Windows dùng "SE Asia Standard Time", Linux (Render) dùng "Asia/Ho_Chi_Minh"
            try
            {
                _vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // Windows
            }
            catch (TimeZoneNotFoundException)
            {
                _vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); // Linux / Docker
            }
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

                // Chờ 1 phút trước khi quét lại
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task CleanupUnpaidOrdersAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. Lấy thời gian hiện tại theo giờ VN (đã fix lỗi timezone)
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

                // 2. Đơn hàng quá hạn 5 phút
                var expirationTime = now.AddMinutes(-5);

                // 3. Tìm đơn hàng
                var expiredOrders = await context.orders
                    .Include(o => o.Payments)
                    .Where(o => o.status == "Pending" && o.orderdate < expirationTime)
                    .ToListAsync();

                // 4. Lọc đơn cần hủy (Không hủy đơn COD)
                var ordersToCancel = expiredOrders
                    .Where(o => !o.Payments.Any(p => p.PaymentMethod == "COD"))
                    .ToList();

                if (ordersToCancel.Any())
                {
                    _logger.LogInformation($"Tìm thấy {ordersToCancel.Count} đơn hàng treo quá hạn. Đang hủy...");

                    foreach (var order in ordersToCancel)
                    {
                        // Cập nhật trạng thái Order
                        order.status = "Đã hủy";

                        // Tạo lịch sử hủy
                        var cancellation = new order_cancellation
                        {
                            OrderId = order.orderid,
                            CancelledBy = order.customerid,
                            CancelReason = "Hủy tự động do quá hạn thanh toán (5 phút)",
                            CancelledAt = now,
                            RefundAmount = 0,
                            RefundStatus = "N/A"
                        };
                        context.order_cancellations.Add(cancellation);

                        // Cập nhật trạng thái Payment
                        foreach (var pay in order.Payments)
                        {
                            if (pay.Status == "Pending")
                            {
                                pay.Status = "Đã Hủy";
                            }
                        }

                        // Hoàn lại mã giảm giá (nếu có)
                        if (order.promotionid.HasValue)
                        {
                            var promotion = await context.promotions.FindAsync(order.promotionid.Value);
                            if (promotion != null)
                            {
                                promotion.CurrentUsageCount--;
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
                    _logger.LogInformation($"Đã hủy thành công {ordersToCancel.Count} đơn hàng.");
                }
            }
        }
    }
}