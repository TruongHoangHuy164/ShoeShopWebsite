using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

// Cấu hình DbContext
builder.Services.AddDbContext<NikeShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NikeShopDb")));

// Cấu hình Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<NikeShopDbContext>();

// Thêm IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Thêm IHttpClientFactory
builder.Services.AddHttpClient();

// Thêm Razor Pages
builder.Services.AddRazorPages();

// Thêm dịch vụ MoMo và VNPay
builder.Services.AddScoped<IMomoService, MomoService>();
builder.Services.AddScoped<IVNPayService, VNPayService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Đảm bảo thứ tự middleware
app.UseSession();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

// Cấu hình route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();