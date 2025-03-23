using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Th�m d?ch v? Session
builder.Services.AddDistributedMemoryCache(); // S? d?ng b? nh? ??m trong b? nh?
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Th?i gian timeout c?a Session (30 ph�t)
    options.Cookie.HttpOnly = true; // Cookie ch? c� th? truy c?p qua HTTP
    options.Cookie.IsEssential = true; // Cookie c?n thi?t ?? tu�n th? GDPR
});

// Th�m DbContext
builder.Services.AddDbContext<NikeShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NikeShopDb")));

// Th�m Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<NikeShopDbContext>();

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Th�m d�ng n�y ?? ph?c v? file t?nh

app.UseRouting();

// Th�m middleware Session
app.UseSession(); // ??m b?o g?i sau UseRouting v� tr??c UseAuthorization

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();

app.Run();