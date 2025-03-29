using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Thêm dịch vụ Session
builder.Services.AddDistributedMemoryCache(); // Sử dụng bộ nhớ đệm trong bộ nhớ cho session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian timeout của session
    options.Cookie.HttpOnly = true; // Bảo mật cookie
    options.Cookie.IsEssential = true; // Đánh dấu cookie là cần thiết (GDPR compliance)
});

// Thêm DbContext
builder.Services.AddDbContext<NikeShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NikeShopDb")));

// Thêm Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<NikeShopDbContext>();

// Thêm IHttpClientFactory để hỗ trợ gửi HTTP request (dùng cho MoMo hoặc VNPay nếu cần)
builder.Services.AddHttpClient();

// Thêm Razor Pages (nếu cần dùng Identity UI hoặc các trang Razor khác)
builder.Services.AddRazorPages();

// Thêm dịch vụ MoMo
builder.Services.AddScoped<IMomoService, MomoService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Phục vụ các file tĩnh như CSS, JS, hình ảnh

app.UseRouting();

// Thêm middleware Session (phải gọi sau UseRouting và trước UseAuthentication/UseAuthorization)
app.UseSession();

// Thêm middleware Authentication và Authorization (Identity yêu cầu)
app.UseAuthentication();
app.UseAuthorization();

// Cấu hình route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Nếu bạn sử dụng Razor Pages (ví dụ: Identity UI)
app.MapRazorPages();

// Chạy ứng dụng
app.Run();