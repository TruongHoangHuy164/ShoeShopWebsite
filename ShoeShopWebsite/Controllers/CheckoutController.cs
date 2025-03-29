using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly NikeShopDbContext _context;

        public CheckoutController(NikeShopDbContext context)
        {
            _context = context;
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

        // GET: Checkout/Index
        public async Task<IActionResult> Index()
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

        // POST: Checkout/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(string paymentMethod)
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
                    return Json(new
                    {
                        success = true,
                        redirectUrl = Url.Action("OrderConfirmation", new { orderId = order.OrderID }),
                        message = "Đơn hàng đã được đặt thành công. Vui lòng chuẩn bị tiền mặt khi nhận hàng!"
                    });

                case "MoMo":
                    // Logic MoMo (giữ nguyên nếu cần)
                    return Json(new { success = false, message = "Phương thức MoMo chưa được triển khai!" });

                case "VNPay":
                    // Logic VNPay (giữ nguyên nếu cần)
                    return Json(new { success = false, message = "Phương thức VNPay chưa được triển khai!" });

                default:
                    return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ!" });
            }
        }

        // GET: Checkout/OrderConfirmation
        public async Task<IActionResult> OrderConfirmation(int orderId)
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
    }
}