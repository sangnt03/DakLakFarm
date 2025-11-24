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
        public async Task<bool> ApprovePayoutRequest(int payoutRequestId, int adminId, string transactionProof)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lấy yêu cầu rút tiền
                var request = await _context.PayoutRequests
                    .FirstOrDefaultAsync(p => p.PayoutRequestId == payoutRequestId);

                // Check trạng thái (chỉ duyệt đơn Pending)
                if (request == null || request.Status != "Pending") return false;

                // 2. Lấy thông tin Farmer để check số dư lần cuối
                var farmer = await _context.users.FindAsync(request.FarmerId);
                if (farmer == null || farmer.balance < request.Amount) return false;

                // 3. Cập nhật trạng thái PayoutRequest (Theo bảng của bạn)
                request.Status = "Completed";
                request.CompletedDate = DateTimeHelper.GetVietnamTime();

                _context.PayoutRequests.Update(request);

                // 4. Trừ tiền Farmer
                farmer.balance -= request.Amount;
                _context.users.Update(farmer);

                // 5. Ghi lịch sử biến động số dư (WalletTransaction)
                var walletTx = new WalletTransaction
                {
                    FarmerId = request.FarmerId,
                    Amount = -request.Amount, // Số âm
                    Type = "Payout",
                    CreateDate = DateTimeHelper.GetVietnamTime(),
                    Description = $"Rút tiền thành công. Mã GD MoMo: {transactionProof}",
                    ReferenceId = request.PayoutRequestId // Link ngược lại bảng request
                };
                _context.WalletTransaction.Add(walletTx);

                // 6. Lưu tất cả và Commit
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
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