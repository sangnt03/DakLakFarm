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

        // 1. Lấy số dư khả dụng
        public async Task<decimal> GetAvailableBalance(int farmerId)
        {
            // - Field: FarmerId, Amount
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

        // 3. Farmer tạo yêu cầu rút tiền
        public async Task<bool> CreatePayoutRequest(int farmerId, decimal amount, string bankDetails)
        {
            // Kiểm tra số dư
            var balance = await GetAvailableBalance(farmerId);
            if (balance < amount) return false;

            // Kiểm tra yêu cầu Pending cũ
            // - Field: Status, FarmerId
            var hasPending = await _context.PayoutRequests
                .AnyAsync(p => p.FarmerId == farmerId && p.Status == "Pending");

            if (hasPending) return false;

            var request = new PayoutRequest
            {
                FarmerId = farmerId,
                Amount = amount,
                Status = "Pending",
                BankDetails = bankDetails, // Lưu chuỗi JSON vào cột jsonb
                RequestDate = DateTimeHelper.GetVietnamTime()
            };

            _context.PayoutRequests.Add(request);
            await _context.SaveChangesAsync();
            return true;
        }

        // 4. Admin DUYỆT yêu cầu -> Trừ tiền ví
        public async Task<bool> ApprovePayoutRequest(int payoutRequestId, int adminId)
        {
            var request = await _context.PayoutRequests.FindAsync(payoutRequestId);
            if (request == null || request.Status != "Pending") return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // A. Update trạng thái PayoutRequest
                request.Status = "Completed";
                request.CompletedDate = DateTimeHelper.GetVietnamTime();

                // B. Tạo giao dịch trừ tiền (WalletTransaction)
                //
                var walletTx = new WalletTransaction
                {
                    FarmerId = request.FarmerId,
                    Amount = -request.Amount, // Số âm để trừ tiền
                    Type = "Payout",
                    ReferenceId = request.PayoutRequestId, // Link tới ID yêu cầu rút
                    Description = $"Rút tiền thành công (Yêu cầu #{request.PayoutRequestId})",
                    CreateDate = DateTimeHelper.GetVietnamTime()
                };

                _context.WalletTransaction.Add(walletTx);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        // 5. Admin TỪ CHỐI yêu cầu
        public async Task<bool> RejectPayoutRequest(int payoutRequestId, string reason)
        {
            var request = await _context.PayoutRequests.FindAsync(payoutRequestId);
            if (request == null || request.Status != "Pending") return false;

            request.Status = "Rejected";
            request.CompletedDate = DateTimeHelper.GetVietnamTime();

            // Có thể lưu lý do từ chối vào một chỗ khác hoặc gửi email thông báo cho Farmer
            // Hiện tại Model PayoutRequest chưa có cột 'Reason', bạn có thể bổ sung sau nếu cần.

            await _context.SaveChangesAsync();
            return true;
        }

        // 6. Lấy danh sách yêu cầu rút tiền của 1 Farmer
        public async Task<List<PayoutRequest>> GetPayoutRequestsByFarmer(int farmerId)
        {
            return await _context.PayoutRequests
                .Where(p => p.FarmerId == farmerId)
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();
        }
    }
}