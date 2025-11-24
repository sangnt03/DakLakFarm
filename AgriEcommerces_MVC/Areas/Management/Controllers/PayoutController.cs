using AgriEcommerces_MVC.Areas.Management.ViewModel;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Service.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Transactions;

namespace AgriEcommerces_MVC.Areas.Management.Controllers
{
    [Area("Management")]
    [Authorize(Roles = "Admin")]
    public class PayoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly WalletService _walletService;
        private readonly ILogger<PayoutController> _logger;

        public PayoutController(
            ApplicationDbContext context,
            WalletService walletService,
            ILogger<PayoutController> logger)
        {
            _context = context;
            _walletService = walletService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string status = "Pending")
        {
            try
            {
                // 1. Lấy danh sách yêu cầu rút tiền theo trạng thái (để hiển thị bảng)
                var requests = await _context.PayoutRequests
                    .Include(p => p.Farmer)
                    .Where(p => p.Status == status)
                    .OrderByDescending(p => p.RequestDate)
                    .ToListAsync();

                var totalCashIn = await _context.Payments
                    .Where(p => p.Status == "Success" || p.Status == "Completed")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                // B. TIỀN RA: Tổng tiền đã chuyển khoản cho Farmer (Payout Completed)
                var totalCashOut = await _context.PayoutRequests
                    .Where(p => p.Status == "Completed")
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;

                // C. SỐ DƯ THỰC TẾ
                var currentAdminBalance = totalCashIn - totalCashOut;

                // Truyền dữ liệu xuống View
                ViewBag.AdminWalletBalance = currentAdminBalance;

                // Đếm số lượng badge cho Tabs
                ViewBag.PendingCount = await _context.PayoutRequests.CountAsync(p => p.Status == "Pending");
                ViewBag.CompletedCount = await _context.PayoutRequests.CountAsync(p => p.Status == "Completed");
                ViewBag.RejectedCount = await _context.PayoutRequests.CountAsync(p => p.Status == "Rejected");
                ViewBag.CurrentStatus = status;

                return View(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payout requests");
                TempData["Error"] = "Có lỗi xảy ra khi tải dữ liệu";
                return View(new List<PayoutRequest>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var request = await _context.PayoutRequests
                    .Include(p => p.Farmer)
                    .FirstOrDefaultAsync(p => p.PayoutRequestId == id);

                if (request == null)
                {
                    TempData["Error"] = "Không tìm thấy yêu cầu rút tiền";
                    return RedirectToAction("Index");
                }

                // Parse thông tin ngân hàng từ JSON
                try
                {
                    var bankDetails = JsonConvert.DeserializeObject<dynamic>(request.BankDetails);
                    ViewBag.BankDetails = bankDetails;
                }
                catch
                {
                    ViewBag.BankDetails = null;
                }

                // Lấy số dư hiện tại của Farmer
                var balance = await _walletService.GetAvailableBalance(request.FarmerId);
                ViewBag.FarmerBalance = balance;

                // Lấy lịch sử giao dịch gần đây
                var recentTransactions = await _context.WalletTransaction
                    .Where(t => t.FarmerId == request.FarmerId)
                    .OrderByDescending(t => t.CreateDate)
                    .Take(5)
                    .ToListAsync();
                ViewBag.RecentTransactions = recentTransactions;

                return View(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading payout request details {id}");
                TempData["Error"] = "Có lỗi xảy ra khi tải chi tiết yêu cầu";
                return RedirectToAction("Index");
            }
        }

        /// Duyệt yêu cầu rút tiền
        /// POST: /Manager/Payout/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string transactionCode)
        {
            try
            {
                var adminId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

                var success = await _walletService.ApprovePayoutRequest(id, adminId, transactionCode);

                if (success)
                {
                    _logger.LogInformation($"Admin {adminId} approved payout request {id}");
                    TempData["Success"] = "Đã duyệt yêu cầu rút tiền thành công. Vui lòng thực hiện chuyển khoản cho Farmer.";
                }
                else
                {
                    TempData["Error"] = "Không thể duyệt yêu cầu này. Vui lòng kiểm tra lại trạng thái.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error approving payout request {id}");
                TempData["Error"] = "Có lỗi xảy ra khi duyệt yêu cầu rút tiền";
            }

            return RedirectToAction("Index");
        }

        /// Từ chối yêu cầu rút tiền
        /// POST: /Manager/Payout/Reject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "Vui lòng nhập lý do từ chối";
                    return RedirectToAction("Details", new { id });
                }

                var success = await _walletService.RejectPayoutRequest(id, reason);

                if (success)
                {
                    var adminId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                    _logger.LogInformation($"Admin {adminId} rejected payout request {id}. Reason: {reason}");
                    TempData["Success"] = "Đã từ chối yêu cầu rút tiền.";
                }
                else
                {
                    TempData["Error"] = "Không thể từ chối yêu cầu này.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting payout request {id}");
                TempData["Error"] = "Có lỗi xảy ra khi từ chối yêu cầu";
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Report(int year = 0, int month = 0)
            {
                try
                {
                    if (year == 0) year = DateTime.Now.Year;
                    if (month == 0) month = DateTime.Now.Month;

                    var startDate = new DateTime(year, month, 1);
                    var endDate = startDate.AddMonths(1);

                    var validOrdersQuery = _context.orderdetails
                        .Include(od => od.order)
                        .Where(od => od.order.orderdate >= startDate &&
                                     od.order.orderdate < endDate &&
                                     od.order.status != "Đã hủy" &&
                                     od.order.status != "Pending"); // Bỏ qua đơn chưa chốt

                    // 1. TỔNG GIÁ TRỊ GIAO DỊCH (GMV) - Tổng tiền khách trả
                    var totalGMV = await validOrdersQuery
                        .SumAsync(od => (decimal?)(od.quantity * od.unitprice)) ?? 0;

                    // 2. DOANH THU THỰC TẾ CỦA ADMIN (Net Revenue)
                    var adminNetRevenue = await validOrdersQuery
                        .SumAsync(od => (decimal?)od.AdminCommission) ?? 0;

                    // 3. DOANH THU THỰC TẾ CỦA FARMER
                    //CẦN SỬA LẠI 
                    var farmerNetRevenue = await validOrdersQuery
                        .SumAsync(od => (decimal?)od.FarmerRevenue) ?? 0;

                    // 4. Các chỉ số về Rút tiền (Giữ nguyên)
                    var pendingPayouts = await _context.PayoutRequests
                        .Where(p => p.Status == "Pending")
                        .SumAsync(p => (decimal?)p.Amount) ?? 0;

                    var completedPayouts = await _context.PayoutRequests
                        .Where(p => p.Status == "Completed" && p.CompletedDate >= startDate && p.CompletedDate < endDate)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0;

                    // 5. Top Farmer
                    var topFarmersData = await _context.WalletTransaction
                        .Where(t => t.Type == "OrderRevenue" && t.CreateDate >= startDate && t.CreateDate < endDate)
                        .GroupBy(t => t.FarmerId)
                        .Select(g => new { FarmerId = g.Key, TotalRevenue = g.Sum(t => t.Amount) })
                        .OrderByDescending(x => x.TotalRevenue)
                        .Take(5)
                        .ToListAsync();

                    var topFarmerDetails = new List<TopFarmerViewModel>();
                    foreach (var item in topFarmersData)
                    {
                        var farmer = await _context.users.FindAsync(item.FarmerId);
                        topFarmerDetails.Add(new TopFarmerViewModel
                        {
                            FarmerName = farmer?.fullname ?? "N/A",
                            FarmerEmail = farmer?.email ?? "N/A",
                            TotalRevenue = item.TotalRevenue
                        });
                    }

                    // --- TRUYỀN DỮ LIỆU SANG VIEW ---
                    ViewBag.Year = year;
                    ViewBag.Month = month;

                    ViewBag.TotalGMV = totalGMV;                 // Tổng tiền hàng toàn sàn
                    ViewBag.AdminNetRevenue = adminNetRevenue;   // Tiền Admin
                    ViewBag.FarmerNetRevenue = farmerNetRevenue; // Tiền Farmer

                    ViewBag.PendingPayouts = pendingPayouts;
                    ViewBag.CompletedPayouts = completedPayouts;

                    ViewBag.TopFarmers = topFarmerDetails;

                    return View();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading report");
                    return View();
                }
            }

    public async Task<IActionResult> ExportReport(int year, int month)
        {
            try
            {
                // TODO: Implement Excel export using EPPlus or ClosedXML
                TempData["Info"] = "Tính năng export đang được phát triển";
                return RedirectToAction("Report", new { year, month });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                TempData["Error"] = "Có lỗi xảy ra khi export báo cáo";
                return RedirectToAction("Report");
            }
        }
    }
}