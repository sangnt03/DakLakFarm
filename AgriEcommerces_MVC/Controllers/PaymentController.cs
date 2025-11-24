using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Service.EmailService;
using AgriEcommerces_MVC.Service.MoMoService;
using AgriEcommerces_MVC.Service.VnPayService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgriEcommerces_MVC.Controllers
{
    [Authorize]
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly VNPayService _vnPayService;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly MoMoService _moMoService;
        public PaymentController(
            ApplicationDbContext context,
            VNPayService vnPayService,
            ILogger<PaymentController> logger,
            IConfiguration configuration,
            IEmailService emailService,
            MoMoService moMoService)

        {
            _context = context;
            _vnPayService = vnPayService;
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
            _moMoService = moMoService;
        }

        [HttpGet]
        public async Task<IActionResult> CreatePayment(int orderId)
        {
            try
            {
                // Debug: Log cấu hình VNPay
                var tmnCode = _configuration["VNPay:TmnCode"];
                var hashSecret = _configuration["VNPay:HashSecret"];

                _logger.LogInformation($"VNPay Config - TmnCode: {tmnCode}, HashSecret exists: {!string.IsNullOrEmpty(hashSecret)}");

                if (string.IsNullOrEmpty(tmnCode) || string.IsNullOrEmpty(hashSecret))
                {
                    _logger.LogError("VNPay configuration is missing!");
                    TempData["Error"] = "Cấu hình thanh toán chưa đầy đủ. Vui lòng liên hệ quản trị viên.";
                    return RedirectToAction("OrderConfirmation", "Order", new { orderId });
                }

                // Lấy thông tin đơn hàng
                var order = await _context.orders
                    .Include(o => o.customer)
                    .FirstOrDefaultAsync(o => o.orderid == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Order {orderId} not found");
                    return NotFound("Đơn hàng không tồn tại");
                }

                // Kiểm tra quyền (Customer chỉ thanh toán đơn hàng của mình)
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                if (order.customerid != currentUserId)
                {
                    _logger.LogWarning($"User {currentUserId} attempted to pay for order {orderId} owned by {order.customerid}");
                    return Forbid();
                }

                // Kiểm tra trạng thái đơn hàng
                if (order.status != "Pending")
                {
                    _logger.LogWarning($"Order {orderId} is not in Pending status. Current status: {order.status}");
                    TempData["Error"] = $"Đơn hàng không ở trạng thái chờ thanh toán (Trạng thái hiện tại: {order.status})";
                    return RedirectToAction("OrderConfirmation", "Order", new { orderId });
                }

                // Kiểm tra đã có Payment chưa
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Pending");

                // Trong hàm CreatePayment(int orderId)

                // Tìm đoạn tạo bản ghi Payment
                if (existingPayment == null)
                {
                    var createDate = DateTimeHelper.GetVietnamTime();
                    var payment = new Payment
                    {
                        OrderId = orderId,
                        PaymentMethod = "VNPay", // <-- Đảm bảo luôn set là VNPay ở đây
                        Amount = order.FinalAmount,
                        Status = "Pending",
                        CreateDate = createDate
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Nếu đã có payment (ví dụ lúc đầu chọn COD xong đổi ý muốn thanh toán VNPay)
                    // Thì cập nhật lại Method
                    existingPayment.PaymentMethod = "VNPay";
                    _context.Payments.Update(existingPayment);
                    await _context.SaveChangesAsync();
                }

                // Lấy IP của khách hàng
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                _logger.LogInformation($"Creating VNPay payment URL for Order {orderId}, Amount: {order.FinalAmount}, IP: {ipAddress}");

                // Tạo URL thanh toán VNPay
                var paymentUrl = _vnPayService.CreatePaymentUrl(
                    orderId,
                    order.FinalAmount,
                    $"Thanh toan don hang {order.ordercode}",
                    ipAddress
                );

                _logger.LogInformation($"Redirecting to VNPay...");

                // Chuyển hướng khách hàng đến trang thanh toán VNPay
                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating payment for Order {orderId}");
                TempData["Error"] = "Có lỗi xảy ra khi tạo thanh toán. Vui lòng thử lại.";
                return RedirectToAction("OrderConfirmation", "Order", new { orderId });
            }
        }

        [HttpGet("Payment/VNPayReturn")]
        [AllowAnonymous]
        public async Task<IActionResult> VNPayReturn()
        {
            // Fix lỗi ngrok khi dev
            Response.Headers["ngrok-skip-browser-warning"] = "true";

            try
            {
                var queryParams = Request.Query;

                // 1. Lấy các tham số quan trọng
                string vnp_SecureHash = queryParams["vnp_SecureHash"]; // Chữ ký của VNPay
                string vnp_ResponseCode = queryParams["vnp_ResponseCode"];
                string vnp_TxnRef = queryParams["vnp_TxnRef"];
                string vnp_TransactionNo = queryParams["vnp_TransactionNo"];

                // 2. Lấy HashSecret từ cấu hình (BẮT BUỘC ĐỂ CHECK CHỮ KÝ)
                string vnp_HashSecret = _configuration["VNPay:HashSecret"];
                bool checkSignature = _vnPayService.ValidateSignature(queryParams, vnp_SecureHash, vnp_HashSecret);

                if (!checkSignature)
                {
                    _logger.LogError($"Invalid Signature for Order {vnp_TxnRef}. Potential tampering detected!");
                    TempData["Error"] = "Lỗi bảo mật: Chữ ký thanh toán không hợp lệ.";
                    return RedirectToAction("Index", "Home");
                }

                int orderId = int.Parse(vnp_TxnRef);

                // Lấy thông tin Order và Payment
                var order = await _context.orders.FindAsync(orderId);
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Pending");

                if (order == null) return NotFound();

                if (vnp_ResponseCode == "00")
                {
                    if (order.status != "Paid")
                    {
                        order.status = "Paid";
                    }

                    if (payment != null)
                    {
                        payment.Status = "Completed";
                    }
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($"Order {orderId} payment successful via VNPay.");
                    TempData["Success"] = "Thanh toán thành công! Đơn hàng của bạn đã được xác nhận.";

                    return RedirectToAction("OrderConfirmation", "Order", new { orderId });
                }
                else
                {
                    var errorMessage = _vnPayService.GetResponseDescription(vnp_ResponseCode);

                    if (payment != null)
                    {
                        payment.Status = "Failed";
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogWarning($"VNPay payment failed for Order {orderId}: {errorMessage}");
                    TempData["Error"] = $"Thanh toán thất bại: {errorMessage}";

                    return RedirectToAction("OrderConfirmation", "Order", new { orderId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing VNPay return");
                TempData["Error"] = "Có lỗi xảy ra khi xử lý kết quả thanh toán.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateMoMoPayment(int orderId)
        {
            var order = await _context.orders.FindAsync(orderId);
            if (order == null || order.status != "Pending") return NotFound();

            // 1. Tạo hoặc cập nhật Payment Record (như logic VNPay cũ)
            var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == "Pending");
            if (existingPayment == null)
            {
                _context.Payments.Add(new Payment
                {
                    OrderId = orderId,
                    PaymentMethod = "MoMo", // Lưu ý method là MoMo
                    Amount = order.FinalAmount,
                    Status = "Pending",
                    CreateDate = DateTimeHelper.GetVietnamTime()
                });
            }
            else
            {
                existingPayment.PaymentMethod = "MoMo";
                _context.Payments.Update(existingPayment);
            }
            await _context.SaveChangesAsync();

            // 2. Gọi Service lấy URL thanh toán
            string payUrl = await _moMoService.CreatePaymentRequest(order.orderid, order.FinalAmount, $"Thanh toan don hang #{order.ordercode}", "Khach hang");

            if (!string.IsNullOrEmpty(payUrl))
                return Redirect(payUrl);

            TempData["Error"] = "Lỗi tạo giao dịch MoMo";
            return RedirectToAction("OrderConfirmation", "Order", new { orderId });
        }

        [HttpGet("Payment/MoMoReturn")]
        [AllowAnonymous]
        public async Task<IActionResult> MoMoReturn()
        {
            string signature = Request.Query["signature"];
            if (!_moMoService.ValidateSignature(Request.Query, signature))
            {
                TempData["Error"] = "Lỗi bảo mật: Chữ ký thanh toán MoMo không hợp lệ!";
                return RedirectToAction("Index", "Home");
            }

            // 2. Lấy các tham số từ URL
            string resultCode = Request.Query["resultCode"];
            string rawOrderId = Request.Query["orderId"];
            string transId = Request.Query["transId"];
            string message = Request.Query["message"];

            int orderId = 0;
            try
            {
                if (rawOrderId.Contains("_"))
                {
                    orderId = int.Parse(rawOrderId.Split('_')[0]);
                }
                else
                {
                    orderId = int.Parse(rawOrderId);
                }
            }
            catch (Exception)
            {
                // Trường hợp mã đơn hàng bị lỗi format lạ
                TempData["Error"] = "Lỗi xử lý mã đơn hàng từ cổng thanh toán.";
                return RedirectToAction("Index", "Home");
            }

            // 4. Tìm đơn hàng và bản ghi Payment trong Database
            var order = await _context.orders.FindAsync(orderId);
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);

            if (resultCode == "0")
            {
                if (order != null)
                {
                    order.status = "Paid";
                    _context.orders.Update(order);
                }

                if (payment != null)
                {
                    payment.Status = "Success";
                    payment.GatewayTransactionCode = transId;
                    _context.Payments.Update(payment);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Thanh toán MoMo thành công!";
            }
            else
            {
                if (payment != null)
                {
                    payment.Status = "Failed";
                    _context.Payments.Update(payment);
                    await _context.SaveChangesAsync();
                }

                if (resultCode == "1006")
                {
                    TempData["Error"] = "Bạn đã hủy giao dịch thanh toán MoMo.";
                }
                else
                {
                    TempData["Error"] = $"Thanh toán thất bại: {message} (Mã lỗi: {resultCode})";
                }
            }
            return RedirectToAction("OrderConfirmation", "Order", new { orderId = orderId });
        }

        //public async Task<IActionResult> PaymentFailed(int orderId, string error)
        //{
        //    var order = await _context.orders.FindAsync(orderId);
        //    if (order == null) return NotFound();

        //    ViewBag.OrderCode = order.ordercode;
        //    ViewBag.Error = error;
        //    ViewBag.OrderId = orderId;

        //    return View();
        //}
    }
}