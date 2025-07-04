using AgriEcommerces_MVC.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Logging;
using AgriEcommerces_MVC.Areas.Farmer.Services;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;

var builder = WebApplication.CreateBuilder(args);

// 1) DbContext với PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// 2) Đăng ký OrderService
builder.Services.AddScoped<IOrderService, OrderService>();

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
});

// 4) Authentication (Cookies)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "DynamicScheme";
    options.DefaultChallengeScheme = "DynamicScheme";
    options.DefaultSignInScheme = "DynamicScheme";
})
.AddPolicyScheme("DynamicScheme", "Dynamic authentication scheme", options =>
{
    // Chọn scheme theo URL prefix
    options.ForwardDefaultSelector = context =>
    {
        // nếu URL bắt đầu bằng /Farmer thì xài FarmerAuth
        if (context.Request.Path.StartsWithSegments("/Farmer", StringComparison.OrdinalIgnoreCase))
            return "FarmerAuth";
        // nếu URL bắt đầu bằng /Management thì xài mân
        if (context.Request.Path.StartsWithSegments("/Management", StringComparison.OrdinalIgnoreCase))
            return "ManagerAuth";

        // mặc định dùng Customer cho tất cả URL khác
        return "Customer";
    };
})

// 2) Scheme “Customer” như trước
.AddCookie("Customer", opts =>
{
    opts.Cookie.Name = ".AgriEcomCustomerAuth";
    opts.LoginPath = "/Account/Login";
    opts.LogoutPath = "/Account/Logout";
    opts.AccessDeniedPath = "/Account/AccessDenied";
    // …các setting khác…
})

// 3) Scheme “FarmerAuth” cho Farmer area
.AddCookie("FarmerAuth", opts =>
{
    opts.Cookie.Name = ".AgriEcomFarmerAuth";
    opts.LoginPath = "/Farmer/FarmerAccount/Login";
    opts.LogoutPath = "/Farmer/FarmerAccount/Logout";
    opts.AccessDeniedPath = "/Farmer/FarmerAccount/AccessDenied";
    // …các setting khác…
})
// 3) Scheme “AdminAuth” cho Admin area
.AddCookie("ManagerAuth", opts =>
 {
     opts.Cookie.Name = ".AgriEcomManagerAuth";
     opts.LoginPath = "/Management/Account/Login";
     opts.LogoutPath = "/Management/Account/Logout";
     opts.AccessDeniedPath = "/Management/Account/AccessDenied";
     // …các setting khác…
 });

builder.Services.AddAuthorization();


// 5) Thêm logging chi tiết
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddAuthorization(); // Thêm dịch vụ ủy quyền

var app = builder.Build();

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