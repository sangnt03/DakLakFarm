using AgriEcommerces_MVC.Service.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(Roles = "Farmer")]
    public class WalletController : Controller
    {
        private readonly WalletService _walletService;
        private readonly ILogger<WalletController> _logger;

        public WalletController(WalletService walletService, ILogger<WalletController> logger)
        {
            _walletService = walletService;
            _logger = logger;
        }

        /// <summary>
        /// Trang dashboard ví của Farmer
        /// GET: /Farmer/Wallet/Index
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

                // Lấy số dư khả dụng
                var balance = await _walletService.GetAvailableBalance(farmerId);

                // Lấy lịch sử giao dịch
                var transactions = await _walletService.GetTransactionHistory(farmerId);

                ViewBag.Balance = balance;
                ViewBag.Transactions = transactions;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wallet dashboard");
                TempData["Error"] = "Có lỗi xảy ra khi tải thông tin ví";
                return RedirectToAction("Index", "Home", new { area = "Farmer" });
            }
        }

        /// <summary>
        /// Trang yêu cầu rút tiền
        /// GET: /Farmer/Wallet/RequestPayout
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RequestPayout()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                var balance = await _walletService.GetAvailableBalance(farmerId);

                ViewBag.Balance = balance;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payout request page");
                TempData["Error"] = "Có lỗi xảy ra";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Xử lý yêu cầu rút tiền
        /// POST: /Farmer/Wallet/RequestPayout
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPayout(decimal amount, string bankName, string accountNumber, string accountName)
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

                // Validate số tiền tối thiểu
                if (amount < 100000)
                {
                    TempData["Error"] = "Số tiền rút tối thiểu là 100.000đ";
                    return RedirectToAction("RequestPayout");
                }

                // Validate thông tin ngân hàng
                if (string.IsNullOrWhiteSpace(bankName) ||
                    string.IsNullOrWhiteSpace(accountNumber) ||
                    string.IsNullOrWhiteSpace(accountName))
                {
                    TempData["Error"] = "Vui lòng điền đầy đủ thông tin ngân hàng";
                    return RedirectToAction("RequestPayout");
                }

                // Tạo JSON cho thông tin ngân hàng
                var bankDetails = JsonConvert.SerializeObject(new
                {
                    bankName = bankName.Trim(),
                    accountNumber = accountNumber.Trim(),
                    accountName = accountName.Trim().ToUpper()
                });

                // Tạo yêu cầu rút tiền
                var success = await _walletService.CreatePayoutRequest(farmerId, amount, bankDetails);

                if (success)
                {
                    _logger.LogInformation($"Farmer {farmerId} created payout request for {amount}");
                    TempData["Success"] = "Yêu cầu rút tiền đã được gửi thành công. Vui lòng chờ quản trị viên xét duyệt (1-3 ngày làm việc).";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["Error"] = "Không thể tạo yêu cầu rút tiền. Có thể do: Số dư không đủ hoặc bạn đang có yêu cầu chờ xử lý.";
                    return RedirectToAction("RequestPayout");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payout request");
                TempData["Error"] = "Có lỗi xảy ra khi tạo yêu cầu rút tiền";
                return RedirectToAction("RequestPayout");
            }
        }

        /// Lịch sử yêu cầu rút tiền
        /// GET: /Farmer/Wallet/PayoutHistory
        public async Task<IActionResult> PayoutHistory()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

                var payoutRequests = await _walletService.GetPayoutRequestsByFarmer(farmerId);

                return View(payoutRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payout history");
                TempData["Error"] = "Có lỗi xảy ra khi tải lịch sử rút tiền";
                return RedirectToAction("Index");
            }
        }
    }
}