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

                // Chờ 5 phút trước khi quét lại
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task CleanupUnpaidOrdersAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. Lấy thời gian hiện tại theo giờ VN
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

                // 2. Đơn hàng quá hạn 5 phút
                var expirationTime = now.AddMinutes(-5);

                // 3. Tìm đơn hàng (bao gồm tất cả các bảng liên quan)
                var expiredOrders = await context.orders
                    .Include(o => o.Payments)
                    .Include(o => o.orderdetails)
                    .Where(o => o.status == "Pending" && o.orderdate < expirationTime)
                    .ToListAsync();

                // 4. Lọc đơn cần xóa (Không xóa đơn COD)
                var ordersToDelete = expiredOrders
                    .Where(o => !o.Payments.Any(p => p.PaymentMethod == "COD"))
                    .ToList();

                if (ordersToDelete.Any())
                {
                    _logger.LogInformation($"Tìm thấy {ordersToDelete.Count} đơn hàng treo quá hạn. Đang xóa...");

                    foreach (var order in ordersToDelete)
                    {
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

                        // Xóa các bản ghi liên quan theo thứ tự
                        // 1. Xóa Payments
                        if (order.Payments.Any())
                        {
                            context.Payments.RemoveRange(order.Payments);
                        }

                        // 2. Xóa OrderDetails
                        if (order.orderdetails != null && order.orderdetails.Any())
                        {
                            context.orderdetails.RemoveRange(order.orderdetails);
                        }

                        // 3. Xóa Order Cancellation (nếu có)
                        var cancellations = await context.order_cancellations
                            .Where(c => c.OrderId == order.orderid)
                            .ToListAsync();
                        if (cancellations.Any())
                        {
                            context.order_cancellations.RemoveRange(cancellations);
                        }

                        // 4. Cuối cùng xóa Order
                        context.orders.Remove(order);
                    }

                    await context.SaveChangesAsync();
                    _logger.LogInformation($"Đã xóa thành công {ordersToDelete.Count} đơn hàng và các bản ghi liên quan.");
                }
            }
        }
    }
}