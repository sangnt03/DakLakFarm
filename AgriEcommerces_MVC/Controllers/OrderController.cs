using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Helpers;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ViewModel;
using AgriEcommerces_MVC.Service.EmailService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPromotionService _promotionService;
    private readonly IEmailService _emailService;
    private readonly ILogger<OrderController> _logger;
    private const string CART_KEY = "Cart";
    private const string BUYNOW_KEY = "Cart_BuyNow";
    private const decimal COMMISSION_RATE = 0.10m; // 10% hoa hồng Admin
    private readonly IServiceScopeFactory _scopeFactory;

    // MÚI GIỜ VIỆT NAM (UTC+7)
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public OrderController(
        ApplicationDbContext db,
        IPromotionService promotionService,
        IEmailService emailService,
        ILogger<OrderController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _promotionService = promotionService;
        _emailService = emailService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // HÀM HỖ TRỢ: Lấy thời gian Việt Nam
    private DateTime GetVietnamTime()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    }

    // [GET] /Order/Index
    public async Task<IActionResult> Index(int? sellerId, bool isBuyNow = false)
    {

        string sessionKey = isBuyNow ? BUYNOW_KEY : CART_KEY;
        // 1. Lấy giỏ hàng
        var cartAll = HttpContext.Session.GetObject<CartViewModel>(sessionKey) ?? new CartViewModel();
        CartViewModel cartForPayment = sellerId.HasValue
            ? new CartViewModel { Items = cartAll.Items.Where(i => i.SellerId == sellerId.Value).ToList() }
            : cartAll;

        if (!cartForPayment.Items.Any())
        {
            if (isBuyNow) return RedirectToAction("Index", "Home");
            TempData["Error"] = "Giỏ hàng trống.";
            return RedirectToAction("Index", "Cart");
        }

        // 2. Lấy thông tin khách hàng
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // 3. Lấy danh sách địa chỉ đã lưu
        var savedAddresses = await _db.customer_addresses
                                      .Where(a => a.user_id == userId)
                                      .OrderByDescending(a => a.is_default)
                                      .ToListAsync();

        var model = new CheckoutViewModel
        {
            Cart = cartForPayment,
            SellerId = sellerId,
            SavedAddresses = savedAddresses,
            FinalAmount = cartForPayment.GrandTotal,
            SelectedAddressId = savedAddresses.FirstOrDefault(a => a.is_default)?.id
                            ?? savedAddresses.FirstOrDefault()?.id,
            IsBuyNow = isBuyNow
        };

        return View(model);
    }

    // [POST] /Order/ApplyPromotion (Dùng cho AJAX)
    [HttpPost]
    public async Task<IActionResult> ApplyPromotion(string code, int? sellerId,bool isBuyNow = false)
    {
        string sessionKey = isBuyNow ? BUYNOW_KEY : CART_KEY;
        var cartAll = HttpContext.Session.GetObject<CartViewModel>(sessionKey) ?? new CartViewModel();
        CartViewModel cartForPayment = sellerId.HasValue
            ? new CartViewModel { Items = cartAll.Items.Where(i => i.SellerId == sellerId.Value).ToList() }
            : cartAll;

        if (!cartForPayment.Items.Any())
        {
            return Json(new { success = false, message = "Giỏ hàng trống." });
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var user = await _db.users.FindAsync(userId);
        string customerType = user?.role ?? "retail";

        var result = await _promotionService.ValidatePromotionAsync(code, cartForPayment, userId, customerType);

        if (!result.IsSuccess)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        decimal finalAmount = cartForPayment.GrandTotal - result.DiscountAmount;

        return Json(new
        {
            success = true,
            message = "Áp dụng mã thành công!",
            discountAmount = result.DiscountAmount,
            discountAmountDisplay = result.DiscountAmount.ToString("N0") + " VNĐ",
            finalAmount = finalAmount,
            finalAmountDisplay = finalAmount.ToString("N0") + " VNĐ",
            promotionCode = result.Promotion.Code
        });
    }

    // [POST] /Order/CreateOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(CheckoutViewModel model)
    {
        ModelState.Remove(nameof(model.Cart));

        // 1. Lấy giỏ hàng và thông tin user
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var user = await _db.users.FindAsync(userId);
        string sessionKey = model.IsBuyNow ? BUYNOW_KEY : CART_KEY;
        var cartAll = HttpContext.Session.GetObject<CartViewModel>(sessionKey) ?? new CartViewModel();

        model.Cart = model.SellerId.HasValue
            ? new CartViewModel { Items = cartAll.Items.Where(i => i.SellerId == model.SellerId.Value).ToList() }
            : cartAll;

        if (!model.Cart.Items.Any())
        {
            ModelState.AddModelError("", "Giỏ hàng của bạn đã bị rỗng hoặc phiên làm việc đã hết hạn.");
        }

        // 2. Xử lý địa chỉ
        customer_address? selectedAddress = null;
        if (!model.SelectedAddressId.HasValue)
        {
            ModelState.AddModelError("SelectedAddressId", "Vui lòng chọn một địa chỉ giao hàng.");
        }
        else
        {
            selectedAddress = await _db.customer_addresses
                .FirstOrDefaultAsync(a => a.id == model.SelectedAddressId.Value && a.user_id == userId);

            if (selectedAddress == null)
            {
                ModelState.AddModelError("", "Địa chỉ đã chọn không hợp lệ.");
            }
        }

        // 3. Xử lý khuyến mãi (Validate lại)
        decimal finalDiscountAmount = 0;
        promotion? appliedPromo = null;
        if (!string.IsNullOrEmpty(model.PromotionCode))
        {
            string customerType = user?.role ?? "retail";
            var promoResult = await _promotionService.ValidatePromotionAsync(model.PromotionCode, model.Cart, userId, customerType);
            if (promoResult.IsSuccess)
            {
                finalDiscountAmount = promoResult.DiscountAmount;
                appliedPromo = promoResult.Promotion;
            }
            else
            {
                ModelState.AddModelError("PromotionCode", promoResult.ErrorMessage);
            }
        }

        // 4. Quay lại view nếu có lỗi
        if (!ModelState.IsValid)
        {
            model.SavedAddresses = await _db.customer_addresses
                                          .Where(a => a.user_id == userId)
                                          .OrderByDescending(a => a.is_default)
                                          .ToListAsync();
            return View("Index", model);
        }

        // 5. Tạo đơn hàng (Order) với trạng thái Pending
        string shippingAddressString = $"{selectedAddress.full_address}, {selectedAddress.ward_commune}, {selectedAddress.district}, {selectedAddress.province_city}";
        DateTime orderDateTime = GetVietnamTime();

        var order = new order
        {
            customerid = userId,
            customername = selectedAddress.recipient_name,
            customerphone = selectedAddress.phone_number,
            shippingaddress = shippingAddressString,
            orderdate = DateTime.SpecifyKind(orderDateTime, DateTimeKind.Unspecified),
            status = "Pending", // ← THAY ĐỔI: Chờ thanh toán thay vì "Chờ duyệt"
            totalamount = model.Cart.GrandTotal,
            discountamount = finalDiscountAmount,
            FinalAmount = model.Cart.GrandTotal - finalDiscountAmount,
            promotionid = appliedPromo?.PromotionId,
            PromotionCode = appliedPromo?.Code,
            ordercode = "TEMP-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
        };

        order.orderdetails = new List<orderdetail>();
        decimal originalCartTotal = model.Cart.Items.Sum(i => i.Quantity * i.UnitPrice);
        foreach (var item in model.Cart.Items)
        {
            var prod = await _db.products.FirstOrDefaultAsync(p => p.productid == item.ProductId);
            if (prod != null)
            {
                prod.quantityavailable -= item.Quantity;
                _db.products.Update(prod);
            }
            decimal itemTotalOriginal = item.Quantity * item.UnitPrice;
            decimal farmerRevenue = itemTotalOriginal * (1 - COMMISSION_RATE);
            decimal itemDiscountShare = 0;
            if (originalCartTotal > 0 && finalDiscountAmount > 0)
            {
                itemDiscountShare = (itemTotalOriginal / originalCartTotal) * finalDiscountAmount;
            }
            decimal adminCommission = (itemTotalOriginal * COMMISSION_RATE) - itemDiscountShare;

            order.orderdetails.Add(new orderdetail
            {
                productid = item.ProductId,
                quantity = item.Quantity,
                unitprice = item.UnitPrice,
                sellerid = prod?.userid ?? 0,
                AdminCommission = adminCommission,
                FarmerRevenue = farmerRevenue
            });
        }

        // 8. Lưu vào CSDL
        _db.orders.Add(order);
        await _db.SaveChangesAsync();

        // Tạo mã đơn hàng chính thức
        order.ordercode = OrderCodeGenerator.GenerateOrderCode_DateId(order.orderid, orderDateTime);
        _db.orders.Update(order);
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Order {order.ordercode} created with status Pending, waiting for payment");

        // 9. Cập nhật lại Session Cart
        if (model.IsBuyNow)
        {
            // Nếu là Mua Ngay -> Xóa luôn session Mua Ngay (vì mỗi lần chỉ mua 1 loại)
            HttpContext.Session.Remove(BUYNOW_KEY);
        }
        else
        {
            // Nếu là Giỏ hàng thường -> Chỉ xóa những món đã thanh toán (theo SellerId)
            var remainingItems = cartAll.Items
                                        .Where(i => !model.SellerId.HasValue || i.SellerId != model.SellerId.Value)
                                        .ToList();
            if (remainingItems.Any())
            {
                cartAll.Items = remainingItems;
                HttpContext.Session.SetObject(CART_KEY, cartAll);
            }
            else
            {
                HttpContext.Session.Remove(CART_KEY);
            }
        }

        // 10. CHUYỂN HƯỚNG ĐẾN TRANG THANH TOÁN
        switch (model.PaymentMethod)
        {
            case "VNPay":
                return RedirectToAction("CreatePayment", "Payment", new { orderId = order.orderid });

            case "MoMo":
                return RedirectToAction("CreateMoMoPayment", "Payment", new { orderId = order.orderid });

            case "COD":
            default:
                // 1. Tạo Payment Pending
                var payment = new Payment
                {
                    OrderId = order.orderid,
                    PaymentMethod = "COD",
                    Amount = order.FinalAmount,
                    Status = "Pending",
                    CreateDate = GetVietnamTime()
                };
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                int orderIdForMail = order.orderid;

                // 3. Gửi Email chạy ngầm (Background Task)
                _ = Task.Run(async () =>
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                        try
                        {
                            var orderInScope = await context.orders
                                .AsNoTracking()
                                .Include(o => o.customer)
                                .Include(o => o.orderdetails).ThenInclude(od => od.product)
                                .FirstOrDefaultAsync(o => o.orderid == orderIdForMail);

                            if (orderInScope != null)
                            {
                                // A. Gửi cho Khách
                                if (orderInScope.customer != null && !string.IsNullOrEmpty(orderInScope.customer.email))
                                {
                                    await emailService.SendOrderConfirmationEmailAsync(orderInScope, orderInScope.customer.email);
                                }

                                // B. Gửi cho Farmer (Nhóm theo người bán)
                                var farmerGroups = orderInScope.orderdetails.GroupBy(od => od.sellerid);
                                foreach (var group in farmerGroups)
                                {
                                    var farmerId = group.Key;
                                    var farmer = await context.users.FindAsync(farmerId);

                                    if (farmer != null && !string.IsNullOrEmpty(farmer.email))
                                    {
                                        await emailService.SendOrderNotificationToFarmerAsync(orderInScope, farmer.email, group.ToList());
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Lỗi gửi email background cho đơn hàng COD #{orderIdForMail}");
                        }
                    }
                });

                TempData["Success"] = "Đặt hàng thành công (Thanh toán khi nhận hàng)!";
                return RedirectToAction("OrderConfirmation", "Order", new { orderId = order.orderid });
        }
    }

    // [GET] /Order/OrderConfirmation
    [Authorize]
    public async Task<IActionResult> OrderConfirmation(int orderId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var order = await _db.orders
            .Include(o => o.orderdetails)
            .ThenInclude(od => od.product)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.orderid == orderId && o.customerid == userId);

        if (order == null)
        {
            return NotFound();
        }

        // XỬ LÝ ĐƠN HÀNG CŨ CHƯA CÓ MÃ
        if (string.IsNullOrEmpty(order.ordercode))
        {
            order.ordercode = OrderCodeGenerator.GenerateOrderCode_DateId(
                order.orderid,
                order.orderdate ?? GetVietnamTime()
            );
            _db.orders.Update(order);
            await _db.SaveChangesAsync();
        }

        // LẤY THÔNG TIN HỦY ĐƠN NẾU CÓ
        if (order.status == "Cancelled")
        {
            ViewBag.Cancellation = await _db.order_cancellations
                .Include(c => c.CancelledByUser)
                .FirstOrDefaultAsync(c => c.OrderId == orderId);
        }

        return View(order);
    }

    // [POST] Hủy đơn hàng - CẬP NHẬT: Chỉ cho phép hủy khi Pending hoặc Chờ duyệt
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int orderId, string cancelReason, string returnUrl = null)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Lấy đơn hàng
        var order = await _db.orders
            .Include(o => o.orderdetails)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.orderid == orderId && o.customerid == userId);

        if (order == null)
        {
            TempData["Error"] = "Không tìm thấy đơn hàng.";
            return RedirectBasedOnReturnUrl(returnUrl, orderId);
        }

        if (order.status != "Pending" && order.status != "Chờ duyệt")
        {
            TempData["Error"] = $"Không thể hủy đơn hàng ở trạng thái '{order.status}'.";
            return RedirectBasedOnReturnUrl(returnUrl, orderId);
        }

        var successfulPayment = order.Payments?.FirstOrDefault(p => p.Status == "Success");
        if (successfulPayment != null)
        {
            TempData["Error"] = "Không thể hủy đơn hàng đã thanh toán thành công. Vui lòng liên hệ hỗ trợ.";
            return RedirectBasedOnReturnUrl(returnUrl, orderId);
        }

        // Cập nhật trạng thái
        order.status = "Cancelled";
        var vnTime = GetVietnamTime();

        // LƯU LỊCH SỬ HỦY ĐƠN
        var cancellation = new order_cancellation
        {
            OrderId = orderId,
            CancelledBy = userId,
            CancelReason = string.IsNullOrWhiteSpace(cancelReason) ? "Không có lý do" : cancelReason,
            CancelledAt = DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified),
            RefundAmount = order.FinalAmount,
            RefundStatus = "N/A"
        };
        _db.order_cancellations.Add(cancellation);

        // Hoàn lại promotion nếu có
        if (order.promotionid.HasValue)
        {
            var promotion = await _db.promotions.FindAsync(order.promotionid.Value);
            if (promotion != null)
            {
                promotion.CurrentUsageCount--;

                var usageHistory = await _db.promotion_usagehistories
                    .FirstOrDefaultAsync(h => h.OrderId == orderId);
                if (usageHistory != null)
                {
                    _db.promotion_usagehistories.Remove(usageHistory);
                }
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Đơn hàng {OrderCode} đã được hủy bởi user {UserId} lúc {Time}",
            order.ordercode, userId, vnTime);

        TempData["Success"] = $"Đơn hàng {order.ordercode} đã được hủy thành công.";
        return RedirectBasedOnReturnUrl(returnUrl, orderId);
    }

    // HÀM HỖ TRỢ: Redirect dựa trên returnUrl
    private IActionResult RedirectBasedOnReturnUrl(string returnUrl, int orderId)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("OrderConfirmation", new { orderId });
    }
}