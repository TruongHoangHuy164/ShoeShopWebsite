using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IMomoService _momoService;

        public CheckoutController(NikeShopDbContext context, IMomoService momoService)
        {
            _context = context;
            _momoService = momoService;
        }

        private string GetSessionId()
        {
            var sessionId = HttpContext.Session.GetString("CartSessionId");
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("CartSessionId", sessionId);
                Console.WriteLine($"Generated new SessionId: {sessionId}"); // Debug
            }
            return sessionId;
        }

        // GET: Checkout/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var sessionId = GetSessionId();
                Console.WriteLine($"Checkout Index - SessionId: {sessionId}"); // Debug

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
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn trống. Vui lòng thêm sản phẩm trước khi thanh toán.";
                    return RedirectToAction("Index", "Cart");
                }

                foreach (var item in cartItems)
                {
                    var stock = item.Product.ProductSizes.FirstOrDefault(ps => ps.SizeID == item.SizeID)?.Stock ?? 0;
                    if (item.Quantity > stock)
                    {
                        TempData["ErrorMessage"] = $"Sản phẩm {item.Product.ProductName} (Kích thước: {item.Size.SizeName}) chỉ còn {stock} sản phẩm trong kho.";
                        return RedirectToAction("Index", "Cart");
                    }
                }

                return View(cartItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Index: {ex.Message}\nStackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Đã có lỗi xảy ra. Vui lòng thử lại sau.";
                return RedirectToAction("Index", "Cart");
            }
        }

        // POST: Checkout/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(string paymentMethod)
        {
            try
            {
                var sessionId = GetSessionId();
                Console.WriteLine($"ProcessPayment - SessionId: {sessionId}, PaymentMethod: {paymentMethod}"); // Debug

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
                    return Json(new { success = false, message = "Giỏ hàng trống!" });
                }

                foreach (var item in cartItems)
                {
                    var stock = item.Product.ProductSizes.FirstOrDefault(ps => ps.SizeID == item.SizeID)?.Stock ?? 0;
                    if (item.Quantity > stock)
                    {
                        return Json(new { success = false, message = $"Sản phẩm {item.Product.ProductName} chỉ còn {stock} sản phẩm trong kho." });
                    }
                }

                var order = new Order
                {
                    TotalPrice = cartItems.Sum(c => c.Product.Price * c.Quantity),
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    PaymentMethod = paymentMethod,
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
                Console.WriteLine($"Order created with ID: {order.OrderID}"); // Debug

                foreach (var item in cartItems)
                {
                    var productSize = item.Product.ProductSizes.First(ps => ps.SizeID == item.SizeID);
                    productSize.Stock -= item.Quantity;
                }

                switch (paymentMethod)
                {
                    case "Cash":
                        order.Status = "Completed";
                        _context.Carts.RemoveRange(cartItems);
                        await _context.SaveChangesAsync();
                        var redirectUrl = Url.Action("OrderConfirmation", new { orderId = order.OrderID });
                        Console.WriteLine($"Redirect URL for Cash: {redirectUrl}"); // Debug
                        return Json(new
                        {
                            success = true,
                            redirectUrl = redirectUrl,
                            message = "Đơn hàng đã được đặt thành công. Vui lòng chuẩn bị tiền mặt khi nhận hàng!"
                        });

                    case "MoMo":
                        var momoResponse = await _momoService.CreatePaymentAsync(order);
                        if (momoResponse.resultCode == 0) // Thành công
                        {
                            return Json(new
                            {
                                success = true,
                                redirectUrl = momoResponse.payUrl,
                                message = "Chuyển hướng đến trang thanh toán MoMo!"
                            });
                        }
                        else
                        {
                            return Json(new { success = false, message = $"Thanh toán MoMo thất bại: {momoResponse.message}" });
                        }

                    case "VNPay":
                        return Json(new { success = false, message = "Phương thức VNPay chưa được triển khai!" });

                    default:
                        return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ!" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessPayment: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Đã có lỗi xảy ra khi xử lý thanh toán: {ex.Message}" });
            }
        }

        // GET: Checkout/PaymentCallback
        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            try
            {
                var response = _momoService.PaymentExecuteAsync(HttpContext.Request.Query);
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderID == int.Parse(response.orderId));

                if (order == null)
                {
                    return NotFound("Không tìm thấy đơn hàng!");
                }

                if (response.resultCode == 0) // Thanh toán thành công
                {
                    order.Status = "Completed";
                    var cartItems = await _context.Carts
                        .Where(c => c.SessionId == GetSessionId())
                        .ToListAsync();
                    _context.Carts.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("OrderConfirmation", new { orderId = order.OrderID });
                }
                else
                {
                    order.Status = "Failed";
                    await _context.SaveChangesAsync();
                    TempData["ErrorMessage"] = "Thanh toán MoMo thất bại: " + response.message;
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PaymentCallback: {ex.Message}\nStackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Đã có lỗi xảy ra khi xử lý callback từ MoMo.";
                return RedirectToAction("Index");
            }
        }

        // POST: Checkout/Notify (IPN - MoMo gọi ngầm)
        [HttpPost]
        public async Task<IActionResult> Notify()
        {
            try
            {
                var response = _momoService.PaymentExecuteAsync(HttpContext.Request.Query);
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderID == int.Parse(response.orderId));

                if (order != null)
                {
                    if (response.resultCode == 0)
                    {
                        order.Status = "Completed";
                    }
                    else
                    {
                        order.Status = "Failed";
                    }
                    await _context.SaveChangesAsync();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Notify: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500);
            }
        }

        // GET: Checkout/OrderConfirmation
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Size)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Color)
                    .FirstOrDefaultAsync(o => o.OrderID == orderId);

                if (order == null)
                {
                    return NotFound("Không tìm thấy đơn hàng!");
                }

                return View(order);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OrderConfirmation: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, "Đã có lỗi xảy ra khi tải hóa đơn.");
            }
        }
    }
}