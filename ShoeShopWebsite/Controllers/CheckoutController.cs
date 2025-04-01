using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IMomoService _momoService;
        private readonly IVNPayService _vnpayService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public CheckoutController(NikeShopDbContext context, IMomoService momoService, IVNPayService vnpayService, IConfiguration configuration, HttpClient httpClient)
        {
            _context = context;
            _momoService = momoService;
            _vnpayService = vnpayService;
            _configuration = configuration;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://provinces.open-api.vn/api/");
        }

        private string GetSessionId()
        {
            var sessionId = HttpContext.Session.GetString("CartSessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartSessionId", sessionId);
            }
            return sessionId;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var sessionId = GetSessionId();
                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductImages)
                    .Include(c => c.Size)
                    .Include(c => c.Color)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductSizes)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn trống.";
                    return RedirectToAction("Index", "Cart");
                }

                var model = new CheckoutViewModel { CartItems = cartItems };

                var provincesResponse = await _httpClient.GetAsync("p");
                if (!provincesResponse.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = $"Không thể tải danh sách tỉnh/thành phố. Mã lỗi: {provincesResponse.StatusCode}";
                    return View(model);
                }

                var provincesJson = await provincesResponse.Content.ReadAsStringAsync();
                var provinces = JsonSerializer.Deserialize<List<Province>>(
                    provincesJson,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                ViewBag.Provinces = provinces ?? new List<Province>();
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi tải trang thanh toán: " + ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDistricts(int provinceId)
        {
            try
            {
                if (provinceId <= 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn tỉnh/thành phố hợp lệ." });
                }

                var response = await _httpClient.GetAsync($"p/{provinceId}?depth=2");
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Không thể tải danh sách quận/huyện. Mã lỗi: {response.StatusCode}" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var province = JsonSerializer.Deserialize<Province>(
                    json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                if (province?.Districts == null || !province.Districts.Any())
                {
                    return Json(new { success = false, message = "Không có quận/huyện nào cho tỉnh/thành phố này." });
                }

                return Json(province.Districts.Select(d => new { code = d.Code, name = d.Name }));
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi tải quận/huyện: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWards(int districtId)
        {
            try
            {
                if (districtId <= 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn quận/huyện hợp lệ." });
                }

                var response = await _httpClient.GetAsync($"d/{districtId}?depth=2");
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Không thể tải danh sách phường/xã. Mã lỗi: {response.StatusCode}" });
                }

                var json = await response.Content.ReadAsStringAsync();
                var district = JsonSerializer.Deserialize<District>(
                    json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                if (district?.Wards == null || !district.Wards.Any())
                {
                    return Json(new { success = false, message = "Không có phường/xã nào cho quận/huyện này." });
                }

                return Json(district.Wards.Select(w => new { code = w.Code, name = w.Name }));
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi tải phường/xã: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(CheckoutViewModel model)
        {
            try
            {
                var sessionId = GetSessionId();
                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                    .Include(c => c.Size)
                    .Include(c => c.Color)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductSizes)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    return BadRequest(new { success = false, message = "Giỏ hàng trống!" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.SelectMany(kvp => kvp.Value.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors) });
                }

                if (model.ProvinceId <= 0 || model.DistrictId <= 0 || model.WardId <= 0)
                {
                    return BadRequest(new { success = false, message = "Vui lòng chọn đầy đủ tỉnh/thành phố, quận/huyện và phường/xã." });
                }

                var province = await GetProvinceName(model.ProvinceId);
                var district = await GetDistrictName(model.DistrictId);
                var ward = await GetWardName(model.WardId);

                if (string.IsNullOrEmpty(province) || string.IsNullOrEmpty(district) || string.IsNullOrEmpty(ward))
                {
                    string errorMessage = "Không thể lấy thông tin địa chỉ. ";
                    if (string.IsNullOrEmpty(province)) errorMessage += $"Tỉnh/thành phố không hợp lệ (ProvinceId: {model.ProvinceId}). ";
                    if (string.IsNullOrEmpty(district)) errorMessage += $"Quận/huyện không hợp lệ (DistrictId: {model.DistrictId}). ";
                    if (string.IsNullOrEmpty(ward)) errorMessage += $"Phường/xã không hợp lệ (WardId: {model.WardId}).";
                    return BadRequest(new { success = false, message = errorMessage });
                }

                var fullAddress = $"{model.AddressDetail}, {ward}, {district}, {province}";
                var order = new Order
                {
                    SessionId = sessionId,
                    FullName = model.FullName,
                    Address = fullAddress,
                    PhoneNumber = model.PhoneNumber,
                    Note = model.Note,
                    TotalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity),
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    PaymentMethod = model.PaymentMethod,
                    OrderDetails = cartItems.Select(c => new OrderDetail
                    {
                        ProductID = c.ProductID,
                        SizeID = c.SizeID,
                        ColorID = c.ColorID,
                        Quantity = c.Quantity,
                        Price = c.Product.Price
                    }).ToList()
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartItems)
                {
                    var productSize = item.Product.ProductSizes.FirstOrDefault(ps => ps.SizeID == item.SizeID);
                    if (productSize == null || productSize.Stock < item.Quantity)
                    {
                        return BadRequest(new { success = false, message = $"Sản phẩm {item.Product.ProductName} (Size: {item.Size.SizeName}) không đủ hàng." });
                    }
                    productSize.Stock -= item.Quantity;
                }

                await _context.SaveChangesAsync();

                switch (model.PaymentMethod)
                {
                    case "Cash":
                        order.Status = "Completed";
                        _context.Carts.RemoveRange(cartItems);
                        await _context.SaveChangesAsync();
                        return Ok(new { success = true, redirectUrl = Url.Action("OrderConfirmation", new { orderId = order.OrderID }), message = "Đặt hàng thành công (COD)!" });

                    case "MoMo":
                        var momoResponse = await _momoService.CreatePaymentAsync(order);
                        if (momoResponse?.resultCode == 0)
                        {
                            return Ok(new { success = true, redirectUrl = momoResponse.payUrl, message = "Chuyển hướng đến MoMo!" });
                        }
                        return BadRequest(new { success = false, message = "Thanh toán MoMo thất bại!" });

                    case "VNPay":
                        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                        if (string.IsNullOrEmpty(ipAddress))
                        {
                            return BadRequest(new { success = false, message = "Không thể lấy địa chỉ IP để thanh toán VNPay." });
                        }
                        var vnpayUrl = _vnpayService.CreatePaymentUrl(order, ipAddress);
                        return Ok(new { success = true, redirectUrl = vnpayUrl, message = "Chuyển hướng đến VNPay!" });

                    default:
                        return BadRequest(new { success = false, message = "Phương thức thanh toán không hợp lệ!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi xử lý thanh toán: {ex.Message}" });
            }
        }

        private async Task<string> GetProvinceName(int provinceId)
        {
            try
            {
                if (provinceId <= 0) return string.Empty;

                var response = await _httpClient.GetAsync($"p/{provinceId}");
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadAsStringAsync();
                var province = JsonSerializer.Deserialize<Province>(
                    json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                return province?.Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetDistrictName(int districtId)
        {
            try
            {
                if (districtId <= 0) return string.Empty;

                var response = await _httpClient.GetAsync($"d/{districtId}");
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadAsStringAsync();
                var district = JsonSerializer.Deserialize<District>(
                    json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                return district?.Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetWardName(int wardId)
        {
            try
            {
                if (wardId <= 0) return string.Empty;

                var response = await _httpClient.GetAsync($"w/{wardId}");
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadAsStringAsync();
                var ward = JsonSerializer.Deserialize<Ward>(
                    json,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                );

                return ward?.Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}