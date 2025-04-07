using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Models.Vnpay;
using ShoeShopWebsite.Services;
using ShoeShopWebsite.Services.VnPay;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ShoeShopWebsite.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IMomoService _momoService;
        private readonly IVNPayService _vnpayService;

        public CheckoutController(NikeShopDbContext context, IMomoService momoService, IVNPayService vnpayService)
        {
            _context = context;
            _momoService = momoService;
            _vnpayService = vnpayService;
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
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Đã xảy ra lỗi khi tải trang thanh toán: {ex.Message}";
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(CheckoutViewModel model)
        {
            var sessionId = GetSessionId();

            try
            {
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
                    TempData["ErrorMessage"] = "Giỏ hàng trống!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra ModelState trước để tận dụng validation từ CheckoutViewModel
                if (!ModelState.IsValid)
                {
                    model.CartItems = cartItems;
                    return View("Index", model);
                }

                // Kiểm tra PhoneNumber (dự phòng nếu validation bị bỏ qua)
                if (string.IsNullOrEmpty(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại là bắt buộc.");
                    model.CartItems = cartItems;
                    return View("Index", model);
                }

                var phoneRegex = new Regex("^0\\d{9}$");
                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Số điện thoại phải bắt đầu bằng 0 và có đúng 10 chữ số.");
                    model.CartItems = cartItems;
                    return View("Index", model);
                }

                if (string.IsNullOrEmpty(model.PaymentMethod))
                {
                    TempData["ErrorMessage"] = "Phương thức thanh toán không được để trống!";
                    model.CartItems = cartItems;
                    return View("Index", model);
                }

                Console.WriteLine($"[ProcessPayment] PaymentMethod received: {model.PaymentMethod}");
                Console.WriteLine($"[ProcessPayment] FullName: {model.FullName}, PhoneNumber: {model.PhoneNumber}, AddressDetail: {model.AddressDetail}, Ward: {model.Ward}, District: {model.District}, Province: {model.Province}");

                var fullAddress = $"{model.AddressDetail ?? ""}, {model.Ward ?? ""}, {model.District ?? ""}, {model.Province ?? ""}";
                var order = new Order
                {
                    SessionId = sessionId,
                    FullName = model.FullName, // Đã được kiểm tra bởi ModelState
                    Address = fullAddress,
                    PhoneNumber = model.PhoneNumber, // Đã kiểm tra null
                    Note = model.Note ?? "",
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
                    var productSize = await _context.ProductSizes
                        .FirstOrDefaultAsync(ps => ps.ProductID == item.ProductID && ps.SizeID == item.SizeID);

                    if (productSize == null)
                    {
                        TempData["ErrorMessage"] = $"Không tìm thấy thông tin kích thước cho sản phẩm {item.Product.ProductName} (Size: {item.Size.SizeName}).";
                        model.CartItems = cartItems;
                        return View("Index", model);
                    }

                    if (productSize.Stock < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Sản phẩm {item.Product.ProductName} (Size: {item.Size.SizeName}) không đủ hàng. Còn lại: {productSize.Stock}.";
                        model.CartItems = cartItems;
                        return View("Index", model);
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
                        TempData["SuccessMessage"] = "Đặt hàng thành công (COD)!";
                        return RedirectToAction("OrderConfirmation", new { orderId = order.OrderID });

                    case "MoMo":
                        var orderInfo = new OrderInfoModel
                        {
                            OrderId = order.OrderID.ToString(),
                            FullName = order.FullName,
                            Amount = order.TotalPrice,
                            OrderInfo = $"Thanh toán đơn hàng #{order.OrderID}"
                        };

                        if (orderInfo.Amount < 1000 || orderInfo.Amount > 50000000)
                        {
                            TempData["ErrorMessage"] = "Số tiền không hợp lệ cho MoMo. Phải từ 1,000 đến 50,000,000 VND.";
                            model.CartItems = cartItems;
                            return View("Index", model);
                        }

                        var momoResponse = await _momoService.CreatePaymentAsync(orderInfo);
                        if (momoResponse?.ErrorCode == 0)
                        {
                            _context.Carts.RemoveRange(cartItems);
                            await _context.SaveChangesAsync();
                            return Redirect(momoResponse.PayUrl);
                        }
                        TempData["ErrorMessage"] = $"Thanh toán MoMo thất bại: {momoResponse?.Message ?? "Không có thông tin lỗi"}";
                        model.CartItems = cartItems;
                        return View("Index", model);

                    case "VNPay":
                        var totalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity);
                        Console.WriteLine($"[VNPay] Raw TotalPrice: {totalPrice}");

                        if (totalPrice < 5000 || totalPrice >= 1000000000)
                        {
                            TempData["ErrorMessage"] = "Số tiền giao dịch không hợp lệ. Số tiền phải từ 5,000 đến dưới 1 tỷ đồng.";
                            model.CartItems = cartItems;
                            return View("Index", model);
                        }

                        var paymentInfo = new PaymentInformationModel
                        {
                            Name = order.FullName,
                            OrderDescription = $"Thanh toan don hang #{order.OrderID}",
                            OrderType = "250000",
                            Amount = (double)(totalPrice)
                        };
                        Console.WriteLine($"[VNPay] Sending data: Name={paymentInfo.Name}, Description={paymentInfo.OrderDescription}, Amount={paymentInfo.Amount}, OrderType={paymentInfo.OrderType}");
                        try
                        {
                            var vnpayUrl = _vnpayService.CreatePaymentUrl(paymentInfo, HttpContext);
                            Console.WriteLine($"[VNPay] Generated URL: {vnpayUrl}");
                            if (string.IsNullOrEmpty(vnpayUrl))
                            {
                                TempData["ErrorMessage"] = "Không thể tạo URL thanh toán VNPay. Vui lòng kiểm tra log.";
                                model.CartItems = cartItems;
                                return View("Index", model);
                            }
                            _context.Carts.RemoveRange(cartItems);
                            await _context.SaveChangesAsync();
                            HttpContext.Session.SetInt32("PendingOrderId", order.OrderID);
                            return Redirect(vnpayUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[VNPay] Error creating URL: {ex.Message}");
                            TempData["ErrorMessage"] = $"Lỗi khi tạo URL thanh toán VNPay: {ex.Message}";
                            model.CartItems = cartItems;
                            return View("Index", model);
                        }

                    default:
                        TempData["ErrorMessage"] = $"Phương thức thanh toán không hợp lệ! Giá trị nhận được: '{model.PaymentMethod}'";
                        model.CartItems = cartItems;
                        return View("Index", model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi xử lý thanh toán: {ex.Message}";
                Console.WriteLine($"[ProcessPayment] Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
                model.CartItems = await _context.Carts
                    .Include(c => c.Product)
                    .Include(c => c.Size)
                    .Include(c => c.Color)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();
                return View("Index", model);
            }
        }

        [HttpGet]
        public IActionResult OrderConfirmation(int orderId)
        {
            var order = _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Color)
                .FirstOrDefault(o => o.OrderID == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "Home");
            }

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> VNPayCallback()
        {
            var response = _vnpayService.PaymentExecute(Request.Query);
            Console.WriteLine($"[VNPayCallback] Response: Success={response.Success}, VnPayResponseCode={response.VnPayResponseCode}");

            if (!response.Success)
            {
                TempData["ErrorMessage"] = "Chữ ký không hợp lệ từ VNPay!";
                return RedirectToAction("Index");
            }

            var orderId = HttpContext.Session.GetInt32("PendingOrderId");
            if (!orderId.HasValue)
            {
                TempData["ErrorMessage"] = "Không thể xác định đơn hàng từ callback!";
                return RedirectToAction("Index");
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId.Value);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng!";
                return RedirectToAction("Index");
            }

            if (response.VnPayResponseCode == "00")
            {
                order.Status = "Completed";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thanh toán VNPay thành công!";
            }
            else
            {
                order.Status = "Failed";
                await _context.SaveChangesAsync();
                TempData["ErrorMessage"] = $"Thanh toán VNPay thất bại! Mã lỗi: {response.VnPayResponseCode}";
            }

            HttpContext.Session.Remove("PendingOrderId");
            return RedirectToAction("OrderConfirmation", new { orderId = order.OrderID });
        }
    }
}