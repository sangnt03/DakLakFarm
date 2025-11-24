using AgriEcommerces_MVC.Service.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;


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


        public async Task<IActionResult> Index()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

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

        [HttpGet]
        public async Task<IActionResult> RequestPayout()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPayout(decimal amount, string bankCode, string bankBin, string accountNumber, string accountName, string bankName) // 1. Thêm bankName vào đây
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Validate số tiền tối thiểu
                if (amount < 100000)
                {
                    TempData["Error"] = "Số tiền rút tối thiểu là 100.000đ";
                    return RedirectToAction("RequestPayout");
                }

                // Validate thông tin ngân hàng
                if (string.IsNullOrWhiteSpace(bankCode) ||
                    string.IsNullOrWhiteSpace(accountNumber) ||
                    string.IsNullOrWhiteSpace(accountName))
                {
                    TempData["Error"] = "Vui lòng điền đầy đủ thông tin ngân hàng";
                    return RedirectToAction("RequestPayout");
                }

                // Tạo Object chứa thông tin
                var bankInfoObj = new
                {
                    BankCode = bankCode,      // VD: VCB (Dùng để tạo QR)
                    BankBin = bankBin,        // VD: 970436 (Dự phòng)
                    BankName = bankName,      // VD: Vietcombank (Để Admin dễ đọc)
                    AccountNumber = accountNumber,
                    AccountName = accountName.ToUpper()
                };

                // 2. Chuyển đổi Object sang chuỗi JSON string
                string bankDetailsJson = JsonConvert.SerializeObject(bankInfoObj);

                // Tạo yêu cầu rút tiền
                // 3. Truyền chuỗi JSON vào Service
                var success = await _walletService.CreatePayoutRequest(farmerId, amount, bankDetailsJson);

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

        public async Task<IActionResult> PayoutHistory()
        {
            try
            {
                var farmerId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

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