using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Models.Vnpay;
using ShoeShopWebsite.Services.VnPay;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ShoeShopWebsite.Services.NewFolder;
using Azure;
using System.IO;

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

                if (!ModelState.IsValid)
                {
                    model.CartItems = cartItems;
                    return View("Index", model);
                }

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

                Console.WriteLine($"[Process оплаты] PaymentMethod received: {model.PaymentMethod}");
                Console.WriteLine($"[ProcessPayment] FullName: {model.FullName}, PhoneNumber: {model.PhoneNumber}, AddressDetail: {model.AddressDetail}, Ward: {model.Ward}, District: {model.District}, Province: {model.Province}");

                var fullAddress = $"{model.AddressDetail ?? ""}, {model.Ward ?? ""}, {model.District ?? ""}, {model.Province ?? ""}";

                // Xử lý mã giảm giá
                string discountCode = HttpContext.Session.GetString("DiscountCode");
                decimal? discountAmount = null;
                DiscountCode discount = null;

                if (!string.IsNullOrEmpty(discountCode))
                {
                    discount = await _context.DiscountCodes
                        .FirstOrDefaultAsync(dc => dc.Code == discountCode && dc.IsActive);

                    if (discount != null && discount.ExpiryDate >= DateTime.Today && discount.StartDate <= DateTime.Today &&
                        (discount.MaxUsage == 0 || discount.UsageCount < discount.MaxUsage))
                    {
                        var totalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity);
                        if (totalPrice >= discount.MinOrderValue)
                        {
                            discountAmount = HttpContext.Session.GetString("DiscountAmount") != null
                                ? decimal.Parse(HttpContext.Session.GetString("DiscountAmount"))
                                : 0;

                            discount.UsageCount++;
                            _context.DiscountCodes.Update(discount);
                        }
                    }
                }

                var order = new Order
                {
                    SessionId = sessionId,
                    FullName = model.FullName,
                    Address = fullAddress,
                    PhoneNumber = model.PhoneNumber,
                    Note = model.Note ?? "",
                    TotalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity) - (discountAmount ?? 0),
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    PaymentMethod = model.PaymentMethod,
                    DiscountCode = discount,
                    DiscountAmount = discountAmount,
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
                Console.WriteLine($"[ProcessPayment] Đơn hàng #{order.OrderID} đã được lưu thành công.");

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
                        HttpContext.Session.Remove("DiscountCode");
                        HttpContext.Session.Remove("DiscountAmount");
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
                            HttpContext.Session.Remove("DiscountCode");
                            HttpContext.Session.Remove("DiscountAmount");
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
                            OrderId = order.OrderID.ToString(),
                            Amount = order.TotalPrice,
                            OrderInfo = $"Thanh toan don hang #{order.OrderID} cua {order.FullName} voi so tien {totalPrice:N0} VND",
                            OrderType = "250000",
                            Name = order.FullName
                        };
                        Console.WriteLine($"[VNPay] Sending data: OrderId={paymentInfo.OrderId}, Amount={paymentInfo.Amount}, OrderInfo={paymentInfo.OrderInfo}, OrderType={paymentInfo.OrderType}, Name={paymentInfo.Name}");
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
                            HttpContext.Session.Remove("DiscountCode");
                            HttpContext.Session.Remove("DiscountAmount");
                            await _context.SaveChangesAsync();
                            HttpContext.Session.SetInt32("PendingOrderId", order.OrderID);
                            Console.WriteLine($"[VNPay] Saved PendingOrderId: {order.OrderID}");
                            return Redirect(vnpayUrl);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[VNPay] Error creating URL: {ex.Message}, StackTrace: {ex.StackTrace}");
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscountCode(string discountCode)
        {
            try
            {
                if (string.IsNullOrEmpty(discountCode))
                {
                    return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });
                }

                discountCode = discountCode.Trim().ToLowerInvariant();
                var discount = await _context.DiscountCodes
                    .FirstOrDefaultAsync(dc => dc.Code == discountCode && dc.IsActive);

                if (discount == null)
                {
                    return Json(new { success = false, message = "Mã giảm giá không hợp lệ hoặc không tồn tại." });
                }

                var currentDate = DateTime.Today;
                if (discount.ExpiryDate < currentDate || discount.StartDate > currentDate)
                {
                    return Json(new { success = false, message = "Mã giảm giá đã hết hạn hoặc chưa bắt đầu." });
                }

                if (discount.MaxUsage > 0 && discount.UsageCount >= discount.MaxUsage)
                {
                    return Json(new { success = false, message = "Mã giảm giá đã đạt giới hạn sử dụng." });
                }

                var sessionId = GetSessionId();
                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    return Json(new { success = false, message = "Giỏ hàng trống." });
                }

                var totalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity);
                if (discount.MinOrderValue > 0 && totalPrice < discount.MinOrderValue)
                {
                    return Json(new { success = false, message = $"Đơn hàng phải có giá trị tối thiểu {discount.MinOrderValue:N0} đ để sử dụng mã này." });
                }

                decimal discountAmount = 0;
                if (discount.DiscountType == DiscountTypeEnum.Percentage)
                {
                    discountAmount = totalPrice * (discount.DiscountValue / 100);
                }
                else if (discount.DiscountType == DiscountTypeEnum.Fixed)
                {
                    discountAmount = discount.DiscountValue;
                }

                if (discountAmount > totalPrice)
                {
                    discountAmount = totalPrice;
                }

                HttpContext.Session.SetString("DiscountCode", discount.Code);
                HttpContext.Session.SetString("DiscountAmount", discountAmount.ToString());

                return Json(new { success = true, message = $"Đã áp dụng mã giảm giá! Giảm {discountAmount:N0} đ.", discountAmount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApplyDiscountCode] Lỗi: {ex.Message}, StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi khi áp dụng mã giảm giá: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallbackVnpay()
        {
            try
            {
                Console.WriteLine("[VNPayCallback] Nhận callback với query: " + Request.QueryString);
                var response = _vnpayService.PaymentExecute(Request.Query);
                Console.WriteLine($"[VNPayCallback] Phản hồi: Success={response.Success}, VnPayResponseCode={response.VnPayResponseCode}, Message={response.Message}");

                if (response.Success && response.VnPayResponseCode == "00")
                {
                    var orderInfo = Request.Query["vnp_OrderInfo"].ToString();
                    Console.WriteLine($"[VNPayCallback] vnp_OrderInfo: {orderInfo}");

                    int orderId = -1;
                    var orderIdMatch = Regex.Match(orderInfo, @"#(\d+)");
                    if (orderIdMatch.Success && int.TryParse(orderIdMatch.Groups[1].Value, out orderId))
                    {
                        Console.WriteLine($"[VNPayCallback] OrderID từ regex: {orderId}");
                    }
                    else
                    {
                        var parts = orderInfo.Split('#');
                        if (parts.Length > 1)
                        {
                            var orderIdPart = parts[1].Split(' ')[0];
                            if (int.TryParse(orderIdPart, out orderId))
                            {
                                Console.WriteLine($"[VNPayCallback] OrderID từ split: {orderId}");
                            }
                        }
                    }

                    var pendingOrderId = HttpContext.Session.GetInt32("PendingOrderId");

                    if (orderId == -1)
                    {
                        if (pendingOrderId.HasValue)
                        {
                            orderId = pendingOrderId.Value;
                            Console.WriteLine($"[VNPayCallback] OrderID từ session (dự phòng): {orderId}");
                        }
                    }

                    if (orderId == -1)
                    {
                        Console.WriteLine("[VNPayCallback] Không thể phân tích OrderID từ vnp_OrderInfo hoặc session");
                        return Content("Lỗi: Không thể phân tích OrderID từ vnp_OrderInfo hoặc session.");
                    }

                    var order = await _context.Orders
                        .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                        .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Size)
                        .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Color)
                        .FirstOrDefaultAsync(o => o.OrderID == orderId);

                    if (order == null)
                    {
                        Console.WriteLine($"[VNPayCallback] Không tìm thấy đơn hàng #{orderId}");
                        return Content($"Lỗi: Không tìm thấy đơn hàng #{orderId}.");
                    }

                    order.Status = "Đã thanh toán";
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"[VNPayCallback] Cập nhật trạng thái đơn hàng #{order.OrderID} thành công");

                    if (pendingOrderId.HasValue && pendingOrderId.Value != orderId)
                    {
                        Console.WriteLine($"[VNPayCallback] PendingOrderId không khớp: Session={pendingOrderId.Value}, Callback={orderId}");
                    }
                    else
                    {
                        Console.WriteLine($"[VNPayCallback] OrderID từ callback: {orderId}, PendingOrderId từ session: {pendingOrderId}");
                    }

                    var vnpAmountRaw = decimal.Parse(Request.Query["vnp_Amount"]);
                    var vnpAmount = vnpAmountRaw / 100;
                    Console.WriteLine($"[VNPayCallback] So sánh số tiền: TotalPrice={order.TotalPrice}, vnp_Amount={vnpAmount}, Difference={Math.Abs(order.TotalPrice - vnpAmount)}");

                    HttpContext.Session.Remove("PendingOrderId");

                    var viewPath = Path.Combine(Directory.GetCurrentDirectory(), "Views", "Checkout", "PaymentCallbackVnpay.cshtml");
                    if (!System.IO.File.Exists(viewPath))
                    {
                        Console.WriteLine($"[VNPayCallback] File view không tồn tại tại: {viewPath}");
                        return Content($"Lỗi: File view PaymentCallbackVnpay.cshtml không tồn tại tại {viewPath}.");
                    }

                    Console.WriteLine("[VNPayCallback] Hiển thị trang PaymentCallbackVnpay.cshtml");
                    return View("~/Views/Checkout/PaymentCallbackVnpay.cshtml", order);
                }
                else
                {
                    Console.WriteLine($"[VNPayCallback] Thanh toán thất bại: Success={response.Success}, VnPayResponseCode={response.VnPayResponseCode}, Message={response.Message}");
                    return Content($"Thanh toán thất bại: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VNPayCallback] Lỗi: {ex.Message}, StackTrace: {ex.StackTrace}");
                return Content($"Lỗi xử lý callback VNPay: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckOrderStatus(int orderId)
        {
            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = $"Không tìm thấy đơn hàng #{orderId}" });
                }

                var isPaid = order.Status == "Đã thanh toán";
                return Json(new
                {
                    success = true,
                    orderId = order.OrderID,
                    status = order.Status,
                    isPaid = isPaid,
                    totalPrice = order.TotalPrice,
                    fullName = order.FullName
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CheckOrderStatus] Lỗi: {ex.Message}, StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Lỗi khi kiểm tra trạng thái đơn hàng: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> OrderSuccess(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                Console.WriteLine($"[OrderSuccess] Không tìm thấy đơn hàng #{orderId}");
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction("Index", "Home");
            }

            Console.WriteLine($"[OrderSuccess] Hiển thị trang Thanh toán thành công cho đơn hàng #{orderId}");
            ViewBag.SuccessMessage = TempData["SuccessMessage"]?.ToString() ?? "Thanh toán thành công!";
            ViewBag.OrderId = order.OrderID;
            ViewBag.Amount = order.TotalPrice;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            var sessionId = GetSessionId();
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Size)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Color)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.SessionId == sessionId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.";
                return RedirectToAction("Index", "Home");
            }

            return View("~/Views/Checkout/OrderConfirmation.cshtml", order);
        }

        [HttpGet]
        [Route("MyOrders")]
        public async Task<IActionResult> MyOrders()
        {
            var sessionId = GetSessionId();
            var orders = await _context.Orders
                .Where(o => o.SessionId == sessionId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View("~/Views/Checkout/MyOrders.cshtml", orders);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderCount()
        {
            var sessionId = GetSessionId();
            var count = await _context.Orders
                .CountAsync(o => o.SessionId == sessionId);
            return Json(count);
        }
    }
}