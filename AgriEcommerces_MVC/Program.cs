using AgriEcommerces_MVC.Areas.Farmer.Services;
using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Service;
using AgriEcommerces_MVC.Service.ChatService;
using AgriEcommerces_MVC.Service.EmailService;
using AgriEcommerces_MVC.Service.MoMoService;
using AgriEcommerces_MVC.Service.ShipService;
using AgriEcommerces_MVC.Service.VnPayService;
using AgriEcommerces_MVC.Service.WalletService;
using AgriEcommerces_MVC.Utilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
// Đăng ký MoMoService
builder.Services.AddScoped<MoMoService>();
// Đăng ký ShippingService
builder.Services.AddScoped<IShippingService, ShippingService>();
// Đăng ký ChatService
builder.Services.AddScoped<IChatService, ChatService>();

// SignalR với cấu hình tối ưu
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Bật detailed errors để debug dễ hơn
    options.MaximumReceiveMessageSize = 102400; // 100KB cho mỗi tin nhắn
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
})
.AddMessagePackProtocol(); // Tối ưu hiệu suất truyền tải

// Đăng ký CustomUserIdProvider để SignalR nhận diện user
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// MVC + Razor Runtime Compilation
builder.Services
    .AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Thêm HttpClient (cần cho Cloudflare và các dịch vụ API khác)
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// 4) Authentication (Cookies)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "DynamicScheme";
    options.DefaultChallengeScheme = "DynamicScheme";
    options.DefaultSignInScheme = "External";
})
.AddPolicyScheme("DynamicScheme", "Dynamic authentication scheme", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // *** QUAN TRỌNG: XỬ LÝ SIGNALR AUTHENTICATION ***
        // SignalR Hub cần xác định đúng scheme dựa trên cookie
        if (context.Request.Path.StartsWithSegments("/chathub", StringComparison.OrdinalIgnoreCase))
        {
            // Kiểm tra cookie theo thứ tự ưu tiên
            if (context.Request.Cookies.ContainsKey(".AgriEcomCustomerAuth"))
                return "Customer";
            if (context.Request.Cookies.ContainsKey(".AgriEcomFarmerAuth"))
                return "FarmerAuth";
            if (context.Request.Cookies.ContainsKey(".AgriEcomManagerAuth"))
                return "ManagerAuth";

            // Mặc định nếu không có cookie nào
            return "Customer";
        }

        // Logic routing cho các controller thông thường
        if (context.Request.Path.StartsWithSegments("/Farmer", StringComparison.OrdinalIgnoreCase))
            return "FarmerAuth";
        if (context.Request.Path.StartsWithSegments("/Management", StringComparison.OrdinalIgnoreCase))
            return "ManagerAuth";

        return "Customer";
    };
})

// 2) Scheme "Customer"
.AddCookie("Customer", opts =>
{
    opts.Cookie.Name = ".AgriEcomCustomerAuth";
    opts.LoginPath = "/Account/Login";
    opts.LogoutPath = "/Account/Logout";
    opts.AccessDeniedPath = "/Account/AccessDenied";
    opts.ExpireTimeSpan = TimeSpan.FromHours(12); // Session timeout
    opts.SlidingExpiration = true; // Tự động gia hạn khi người dùng active

    //  logic xử lý Ajax 
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

// 3) Scheme "FarmerAuth" cho Farmer area
.AddCookie("FarmerAuth", opts =>
{
    opts.Cookie.Name = ".AgriEcomFarmerAuth";
    opts.LoginPath = "/Farmer/FarmerAccount/Login";
    opts.LogoutPath = "/Farmer/FarmerAccount/Logout";
    opts.AccessDeniedPath = "/Farmer/FarmerAccount/AccessDenied";
    opts.ExpireTimeSpan = TimeSpan.FromHours(12);
    opts.SlidingExpiration = true;

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

// 4) Scheme "ManagerAuth" cho Management area
.AddCookie("ManagerAuth", opts =>
{
    opts.Cookie.Name = ".AgriEcomManagerAuth";
    opts.LoginPath = "/Management/Account/Login";
    opts.LogoutPath = "/Management/Account/Logout";
    opts.AccessDeniedPath = "/Management/Account/AccessDenied";
    opts.ExpireTimeSpan = TimeSpan.FromHours(12);
    opts.SlidingExpiration = true;

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
.AddCookie("External", opts =>
{
    opts.Cookie.Name = ".AgriEcomExternalAuth";
    opts.ExpireTimeSpan = TimeSpan.FromMinutes(10); // Cookie tạm thời
})

// cấu hình Google
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

var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Xóa giới hạn IP để chấp nhận Proxy của Render (vì IP Render thay đổi liên tục)
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

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
app.UseAuthentication();
app.UseAuthorization();

// Map các route cho Areas
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

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// *** QUAN TRỌNG: Map SignalR Hub ***
// Đảm bảo route này được khai báo SAU khi UseAuthentication và UseAuthorization
app.MapHub<ChatHub>("/chathub", options =>
{
    // Cấu hình cho WebSocket
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;

    // Cấu hình CORS nếu cần (cho development)
    options.AllowStatefulReconnects = true;
});

app.Run();