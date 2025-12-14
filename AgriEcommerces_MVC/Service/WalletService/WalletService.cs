using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Service.WalletService
{
    public class WalletService
    {
        private readonly ApplicationDbContext _context;

        public WalletService(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. TÍNH SỐ DƯ
        public async Task<decimal> GetAvailableBalance(int farmerId)
        {
            // Cộng tất cả dòng tiền (Dương là thu nhập, Âm là rút tiền)
            var balance = await _context.WalletTransaction
                .Where(t => t.FarmerId == farmerId)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return balance;
        }

        // 2. Lấy lịch sử giao dịch
        public async Task<List<WalletTransaction>> GetTransactionHistory(int farmerId)
        {
            return await _context.WalletTransaction
                .Where(t => t.FarmerId == farmerId)
                .OrderByDescending(t => t.CreateDate)
                .ToListAsync();
        }

        // 3. Tạo yêu cầu rút tiền
        public async Task<bool> CreatePayoutRequest(int farmerId, decimal amount, string bankDetails)
        {
            // Gọi hàm tính số dư thay vì lấy từ User
            var balance = await GetAvailableBalance(farmerId);

            // Check: Tiền thực có < Tiền muốn rút
            if (balance < amount) return false;

            // Check: Đang có đơn pending
            var hasPending = await _context.PayoutRequests
                .AnyAsync(p => p.FarmerId == farmerId && p.Status == "Pending");

            if (hasPending) return false;

            var request = new PayoutRequest
            {
                FarmerId = farmerId,
                Amount = amount,
                Status = "Pending",
                BankDetails = bankDetails,
                RequestDate = DateTimeHelper.GetVietnamTime()
            };

            _context.PayoutRequests.Add(request);
            await _context.SaveChangesAsync();
            return true;
        }

        // 4. Admin DUYỆT yêu cầu 
        public async Task<(bool Success, string Message)> ApprovePayoutRequest(int payoutRequestId, int adminId, string transactionProof)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.PayoutRequests
                    .FirstOrDefaultAsync(p => p.PayoutRequestId == payoutRequestId);

                if (request == null) return (false, "Không tìm thấy yêu cầu.");
                if (request.Status != "Pending") return (false, "Yêu cầu không ở trạng thái chờ duyệt.");

                // Check số dư lại lần cuối (quan trọng)
                var currentBalance = await GetAvailableBalance(request.FarmerId);
                if (currentBalance < request.Amount)
                {
                    return (false, $"Số dư hiện tại ({currentBalance:N0}đ) không đủ để duyệt khoản rút {request.Amount:N0}đ.");
                }

                // Cập nhật trạng thái
                request.Status = "Completed";
                request.CompletedDate = DateTimeHelper.GetVietnamTime();
                _context.PayoutRequests.Update(request);

                // --- QUAN TRỌNG: TẠO GIAO DỊCH ÂM ĐỂ TRỪ TIỀN ---
                // (Không cần update bảng User nữa)
                var walletTx = new WalletTransaction
                {
                    FarmerId = request.FarmerId,
                    Amount = -request.Amount, // Số ÂM
                    Type = "Payout",
                    CreateDate = DateTimeHelper.GetVietnamTime(),
                    Description = $"Rút tiền thành công. Mã GD Admin: {transactionProof}",
                    ReferenceId = request.PayoutRequestId
                };
                _context.WalletTransaction.Add(walletTx);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Duyệt thành công");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Lỗi hệ thống: " + ex.Message);
            }
        }

        // 5. Admin TỪ CHỐI yêu cầu
        public async Task<bool> RejectPayoutRequest(int payoutRequestId, string reason)
        {
            var request = await _context.PayoutRequests.FindAsync(payoutRequestId);
            if (request == null || request.Status != "Pending") return false;

            request.Status = "Rejected";
            request.CompletedDate = DateTimeHelper.GetVietnamTime();

            await _context.SaveChangesAsync();
            return true;
        }

        // 6. Lấy danh sách yêu cầu
        public async Task<List<PayoutRequest>> GetPayoutRequestsByFarmer(int farmerId)
        {
            return await _context.PayoutRequests
                .Where(p => p.FarmerId == farmerId)
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();
        }

        // 7. Cộng tiền doanh thu 
        public async Task ProcessOrderRevenue(int orderId)
        {
            var order = await _context.orders
                .Include(o => o.orderdetails)
                .FirstOrDefaultAsync(o => o.orderid == orderId);

            if (order == null) return;

            var farmerGroups = order.orderdetails
                .GroupBy(od => od.sellerid)
                .Select(g => new
                {
                    FarmerId = g.Key,
                    TotalRevenue = g.Sum(od => od.FarmerRevenue)
                })
                .ToList();

            foreach (var item in farmerGroups)
            {
                bool alreadyProcessed = await _context.WalletTransaction
                    .AnyAsync(t => t.ReferenceId == order.orderid
                                   && t.FarmerId == item.FarmerId
                                   && t.Type == "OrderRevenue");

                if (!alreadyProcessed)
                {
                    
                    var walletTx = new WalletTransaction
                    {
                        FarmerId = item.FarmerId,
                        Amount = item.TotalRevenue, // Số DƯƠNG
                        Type = "OrderRevenue",
                        ReferenceId = order.orderid,
                        Description = $"Thu nhập từ đơn hàng #{order.ordercode}",
                        CreateDate = DateTimeHelper.GetVietnamTime()
                    };
                    _context.WalletTransaction.Add(walletTx);

                    
                }
            }
            await _context.SaveChangesAsync();
        }
    }
}