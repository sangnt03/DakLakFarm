using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
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

        /// <summary>
        /// Lấy số dư khả dụng của Farmer
        /// </summary>
        public async Task<decimal> GetAvailableBalance(int farmerId)
        {
            var balance = await _context.WalletTransaction
                .Where(t => t.FarmerId == farmerId)
                .SumAsync(t => t.Amount);

            return balance;
        }

        /// <summary>
        /// Lấy lịch sử giao dịch ví
        /// </summary>
        public async Task<List<Models.WalletTransaction>> GetTransactionHistory(int farmerId, int pageSize = 20, int page = 1)
        {
            return await _context.WalletTransaction
                .Where(t => t.FarmerId == farmerId)
                .OrderByDescending(t => t.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Tạo yêu cầu rút tiền
        /// </summary>
        public async Task<bool> CreatePayoutRequest(int farmerId, decimal amount, string bankDetails)
        {
            // Kiểm tra số dư khả dụng
            var availableBalance = await GetAvailableBalance(farmerId);

            if (availableBalance < amount)
            {
                return false; // Số dư không đủ
            }

            // Kiểm tra có yêu cầu đang chờ xử lý không
            var hasPendingRequest = await _context.PayoutRequests
                .AnyAsync(p => p.FarmerId == farmerId && p.Status == "Pending");

            if (hasPendingRequest)
            {
                return false; // Chỉ cho phép 1 yêu cầu Pending tại một thời điểm
            }

            // ✅ SỬ DỤNG DateTimeHelper
            var payoutRequest = new Models.PayoutRequest
            {
                FarmerId = farmerId,
                Amount = amount,
                Status = "Pending",
                BankDetails = bankDetails,
                RequestDate = DateTimeHelper.GetVietnamTime()
            };

            _context.PayoutRequests.Add(payoutRequest);
            await _context.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Admin duyệt yêu cầu rút tiền
        /// </summary>
        public async Task<bool> ApprovePayoutRequest(int payoutRequestId, int adminId)
        {
            var request = await _context.PayoutRequests
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.PayoutRequestId == payoutRequestId);

            if (request == null || request.Status != "Pending")
            {
                return false;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ✅ SỬ DỤNG DateTimeHelper
                request.Status = "Completed";
                request.CompletedDate = DateTimeHelper.GetVietnamTime();

                // Tạo giao dịch trừ tiền trong ví Farmer
                var walletTransaction = new Models.WalletTransaction
                {
                    FarmerId = request.FarmerId,
                    Amount = -request.Amount, // Số âm để trừ tiền
                    Type = "Payout",
                    ReferenceId = request.PayoutRequestId,
                    Description = $"Rút tiền vào tài khoản ngân hàng",
                    CreateDate = DateTimeHelper.GetVietnamTime()
                };

                _context.WalletTransaction.Add(walletTransaction);

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

        /// <summary>
        /// Admin từ chối yêu cầu rút tiền
        /// </summary>
        public async Task<bool> RejectPayoutRequest(int payoutRequestId, string reason)
        {
            var request = await _context.PayoutRequests
                .FirstOrDefaultAsync(p => p.PayoutRequestId == payoutRequestId);

            if (request == null || request.Status != "Pending")
            {
                return false;
            }

            // ✅ SỬ DỤNG DateTimeHelper
            request.Status = "Rejected";
            request.CompletedDate = DateTimeHelper.GetVietnamTime();

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Lấy danh sách yêu cầu rút tiền của một Farmer
        /// </summary>
        public async Task<List<Models.PayoutRequest>> GetPayoutRequestsByFarmer(int farmerId)
        {
            return await _context.PayoutRequests
                .Where(p => p.FarmerId == farmerId)
                .OrderByDescending(p => p.RequestDate)
                .ToListAsync();
        }
    }
}