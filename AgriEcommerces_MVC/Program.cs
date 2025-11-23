using AgriEcommerces_MVC.Areas.Farmer.Services;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Service.EmailService;
using AgriEcommerces_MVC.Service.VnPayService;
using AgriEcommerces_MVC.Service.WalletService;
using AgriEcommerces_MVC.Service;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.HttpOverrides; 

// Bắt buộc có dòng này để PostgreSQL chấp nhận kiểu DateTime cũ (tránh lỗi crash Service)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1) DbContext với PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// Đăng ký OrderService
builder.Services.AddScoped<IOrderService, OrderService>();
// Đăng ký CheckoutService
builder.Services.AddScoped<IPromotionService, PromotionService>();
// Đăng ký EmailService
builder.Services.AddScoped<IEmailService, EmailService>();
// Đăng ký VnPayService
builder.Services.AddScoped<VNPayService>();
// Đăng ký WalletService
builder.Services.AddScoped<WalletService>();
// Đăng ký UnpaidOrderCleanupService chạy nền tự động xóa đơn hàng chưa thanh toán sau 5 phút với phương thức VNPAY,...
builder.Services.AddHostedService<UnpaidOrderCleanupService>();


// 2) MVC + Razor Runtime Compilation
builder.Services
    .AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// 3) Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Bắt buộc dùng HTTPS
});

// Thêm HttpClient (cần cho Cloudflare và các dịch vụ API khác)
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// 1. Đọc Google Keys từ secrets.json (đặt lên trước khi dùng)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 4) Authentication (Cookies) - Chỉ một khối duy nhất
builder.Services.AddAuthentication(options =>
{
    // Giữ nguyên DynamicScheme của bạn
    options.DefaultScheme = "DynamicScheme";
    options.DefaultChallengeScheme = "DynamicScheme";

    options.DefaultSignInScheme = "External";
})
.AddPolicyScheme("DynamicScheme", "Dynamic authentication scheme", options =>
{
    // Giữ nguyên logic chọn scheme theo URL prefix của bạn
    options.ForwardDefaultSelector = context =>
    {
        // nếu URL bắt đầu bằng /Farmer thì xài FarmerAuth
        if (context.Request.Path.StartsWithSegments("/Farmer", StringComparison.OrdinalIgnoreCase))
            return "FarmerAuth";
        // nếu URL bắt đầu bằng /Management thì xài manager
        if (context.Request.Path.StartsWithSegments("/Management", StringComparison.OrdinalIgnoreCase))
            return "ManagerAuth";

        // mặc định dùng Customer cho tất cả URL khác
        return "Customer";
    };
})

// 2) Scheme "Customer" (Giữ nguyên bản CÓ AJAX của bạn)
.AddCookie("Customer", opts =>
{
    opts.Cookie.Name = ".AgriEcomCustomerAuth";
    opts.LoginPath = "/Account/Login";
    opts.LogoutPath = "/Account/Logout";
    opts.AccessDeniedPath = "/Account/AccessDenied";

    // Giữ nguyên logic xử lý Ajax quan trọng của bạn
    opts.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            // Kiểm tra nếu là Ajax request hoặc request đến Cart controller
            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var isCartRequest = context.Request.Path.StartsWithSegments("/Cart");

            if (isAjax || isCartRequest)
            {
                // Trả về 401 thay vì redirect
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            // Request thường thì redirect đến login
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var isCartRequest = context.Request.Path.StartsWithSegments("/Cart");

            if (isAjax || isCartRequest)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
})

// 3) Scheme "FarmerAuth" cho Farmer area (Giữ nguyên)
.AddCookie("FarmerAuth", opts =>
{
    opts.Cookie.Name = ".AgriEcomFarmerAuth";
    opts.LoginPath = "/Farmer/FarmerAccount/Login";
    opts.LogoutPath = "/Farmer/FarmerAccount/Logout";
    opts.AccessDeniedPath = "/Farmer/FarmerAccount/AccessDenied";

    // Xử lý Ajax requests cho Farmer area
    opts.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjax)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
})

// 4) Scheme "ManagerAuth" cho Management area (Giữ nguyên)
.AddCookie("ManagerAuth", opts =>
{
    opts.Cookie.Name = ".AgriEcomManagerAuth";
    opts.LoginPath = "/Management/Account/Login";
    opts.LogoutPath = "/Management/Account/Logout";
    opts.AccessDeniedPath = "/Management/Account/AccessDenied";

    // Xử lý Ajax requests cho Management area
    opts.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (isAjax)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };
})

// 5) Thêm Cookie "External" tạm thời (cho Google)
// (Đây là phần được thêm vào chuỗi)
.AddCookie("External")

// 6) Thêm cấu hình Google
// (Đây là phần được thêm vào chuỗi)
.AddGoogle(options =>
{
    if (string.IsNullOrEmpty(googleClientId) || string.IsNullOrEmpty(googleClientSecret))
    {
        throw new InvalidOperationException("Google ClientId hoặc ClientSecret chưa được cấu hình.");
    }
    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;
    // Đường dẫn /signin-google được middleware xử lý tự động
});


builder.Services.AddAuthorization();

// 5) Thêm logging chi tiết
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// 6) Chỉ bật Developer Exception Page khi đang dev
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 7) HTTPS + Static Files + Session
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();

// 8) Routing + Auth
app.UseRouting();
app.UseAuthentication(); // Đảm bảo middleware xác thực được gọi
app.UseAuthorization(); // Đảm bảo middleware ủy quyền được gọi

// 9) Map area routes trước route default
app.MapAreaControllerRoute(
    name: "farmer_area",
    areaName: "Farmer",
    pattern: "Farmer/{controller=FarmerAccount}/{action=Login}/{id?}"
);
app.MapAreaControllerRoute(
    name: "manager_area",
    areaName: "Management",
    pattern: "Management/{controller=Account}/{action=Login}/{id?}"
);
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();