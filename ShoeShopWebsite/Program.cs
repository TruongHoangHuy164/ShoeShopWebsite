using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Services;
using ShoeShopWebsite.Hubs;
using ShoeShopWebsite.Services.NewFolder;
using ShoeShopWebsite.Services.VnPay;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Đăng ký dịch vụ Momo
builder.Services.AddScoped<IMomoService, MomoService>();

// Thêm dịch vụ Controller và View
builder.Services.AddControllersWithViews();
//<<<<<<< Hoang
builder.Services.AddAntiforgery();
//=======

// Đăng ký dịch vụ Email
builder.Services.AddScoped<IEmailService, EmailService>();

//>>>>>>> master
// Cấu hình Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Cấu hình Cookie Policy
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

// Cấu hình CultureInfo và Request Localization
var cultureInfo = new CultureInfo("vi-VN")
{
    NumberFormat = { NumberDecimalSeparator = "." },
    DateTimeFormat = { ShortDatePattern = "dd/MM/yyyy" } // Đổi thành định dạng phù hợp với input type="date"
};
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultureInfo);
    options.SupportedCultures = new[] { cultureInfo };
    options.SupportedUICultures = new[] { cultureInfo };
});

// Cấu hình DbContext
builder.Services.AddDbContext<NikeShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NikeShopDb")));

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<NikeShopDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnSignedIn = async context =>
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.HttpContext.User);
        if (user != null && await userManager.IsInRoleAsync(user, SD.Role_Admin))
        {
            context.HttpContext.Response.Redirect("/Admin/AdminDashboard");
        }
        else
        {
            context.HttpContext.Response.Redirect("/Home/Index");
        }
    };
});
// Cấu hình Google Authentication
builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        var googleAuthNSection = builder.Configuration.GetSection("Authentication:Google");
        googleOptions.ClientId = googleAuthNSection["ClientId"];
        googleOptions.ClientSecret = googleAuthNSection["ClientSecret"];
        googleOptions.CallbackPath = "/signin-google"; // Đảm bảo khớp với route
        googleOptions.SignInScheme = IdentityConstants.ExternalScheme;
    });

// Thêm IHttpContextAccessor và IHttpClientFactory
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Thêm Razor Pages
builder.Services.AddRazorPages();

// Cấu hình dịch vụ VNPay
builder.Services.AddScoped<IVNPayService, VnPayService>();

// Thêm SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// Đảm bảo thứ tự middleware
app.UseRequestLocalization(); // Thêm middleware localization
app.UseCookiePolicy();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ChatHub>("/chatHub");

// Cấu hình định tuyến khu vực và default
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
    );

    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}"
    );
});

app.MapRazorPages();

// Route đăng nhập Google
app.MapGet("/signin-google", async (HttpContext context) =>
{
    await context.ChallengeAsync(GoogleDefaults.AuthenticationScheme);
});

app.Run();