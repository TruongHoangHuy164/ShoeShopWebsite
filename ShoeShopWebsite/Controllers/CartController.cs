using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class CartController : Controller
    {
        private readonly NikeShopDbContext _context;
        private const string SessionIdKey = "CartSessionId";

        public CartController(NikeShopDbContext context)
        {
            _context = context;
        }

        private string GetSessionId()
        {
            var sessionId = HttpContext.Session.GetString(SessionIdKey);
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                HttpContext.Session.SetString(SessionIdKey, sessionId);
                Console.WriteLine($"Generated new SessionId: {sessionId}");
            }
            return sessionId;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int sizeId, int? colorId, int quantity = 1)
        {
            try
            {
                // Kiểm tra sản phẩm
                var product = await _context.Products
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == productId);

                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });
                }

                // Kiểm tra kích thước
                var sizeExists = product.ProductSizes.Any(ps => ps.SizeID == sizeId);
                if (!sizeExists)
                {
                    return Json(new { success = false, message = "Kích thước không hợp lệ!" });
                }

                // Kiểm tra màu sắc (nếu có)
                if (colorId.HasValue)
                {
                    var colorExists = product.ProductColors.Any(pc => pc.ColorID == colorId.Value);
                    if (!colorExists)
                    {
                        return Json(new { success = false, message = "Màu sắc không hợp lệ!" });
                    }
                }

                // Kiểm tra tồn kho
                var stock = product.ProductSizes.First(ps => ps.SizeID == sizeId).Stock;
                if (quantity > stock)
                {
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho! Chỉ còn {stock} sản phẩm." });
                }

                var sessionId = GetSessionId();

                // Tìm hoặc tạo mới cart item
                var cartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.ProductID == productId && c.SizeID == sizeId && c.ColorID == colorId && c.SessionId == sessionId);

                if (cartItem == null)
                {
                    cartItem = new Cart
                    {
                        ProductID = productId,
                        SizeID = sizeId,
                        ColorID = colorId,
                        Quantity = quantity,
                        SessionId = sessionId
                    };
                    _context.Carts.Add(cartItem);
                }
                else
                {
                    var newQuantity = cartItem.Quantity + quantity;
                    if (newQuantity > stock)
                    {
                        return Json(new { success = false, message = $"Số lượng vượt quá tồn kho! Chỉ còn {stock - cartItem.Quantity} sản phẩm khả dụng." });
                    }
                    cartItem.Quantity = newQuantity;
                }

                await _context.SaveChangesAsync();

                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == sessionId)
                    .SumAsync(c => c.Quantity);

                return Json(new { success = true, message = "Sản phẩm đã được thêm vào giỏ hàng!", cartCount = cartCount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddToCart: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra. Vui lòng thử lại sau!" });
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var sessionId = GetSessionId();
                Console.WriteLine($"Index - SessionId: {sessionId}");

                var cartItems = await _context.Carts
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductImages)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductSizes)
                        .ThenInclude(ps => ps.Size)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductColors)
                        .ThenInclude(pc => pc.Color)
                    .Include(c => c.Size)
                    .Include(c => c.Color)
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                Console.WriteLine($"Index - Number of cart items: {cartItems.Count}");
                return View(cartItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Index: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Lỗi: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            try
            {
                var sessionId = GetSessionId();
                var cartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartID == cartId && c.SessionId == sessionId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng!" });
                }

                _context.Carts.Remove(cartItem);
                await _context.SaveChangesAsync();

                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == sessionId)
                    .SumAsync(c => c.Quantity);

                return Json(new { success = true, message = "Sản phẩm đã được xóa khỏi giỏ hàng!", cartCount = cartCount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RemoveFromCart: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra. Vui lòng thử lại sau!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCartItem(int cartId, int? sizeId, int? colorId, int? quantity)
        {
            try
            {
                var sessionId = GetSessionId();
                Console.WriteLine($"UpdateCartItem - SessionId: {sessionId}, CartId: {cartId}, SizeId: {sizeId}, ColorId: {colorId}, Quantity: {quantity}");

                var cartItem = await _context.Carts
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductSizes)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductColors)
                    .FirstOrDefaultAsync(c => c.CartID == cartId && c.SessionId == sessionId);

                if (cartItem == null)
                {
                    Console.WriteLine("Cart item not found");
                    return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng!" });
                }

                var product = cartItem.Product;

                // Cập nhật kích thước
                if (sizeId.HasValue)
                {
                    var sizeExists = product.ProductSizes.Any(ps => ps.SizeID == sizeId.Value);
                    if (!sizeExists)
                    {
                        return Json(new { success = false, message = "Kích thước không hợp lệ!" });
                    }
                    cartItem.SizeID = sizeId.Value;
                }

                // Cập nhật màu sắc
                if (colorId.HasValue)
                {
                    var colorExists = product.ProductColors.Any(pc => pc.ColorID == colorId.Value);
                    if (!colorExists)
                    {
                        return Json(new { success = false, message = "Màu sắc không hợp lệ!" });
                    }
                    cartItem.ColorID = colorId.Value;
                }
                else if (colorId == null)
                {
                    cartItem.ColorID = null;
                }

                // Cập nhật số lượng
                if (quantity.HasValue)
                {
                    if (quantity.Value <= 0)
                    {
                        return Json(new { success = false, message = "Số lượng phải lớn hơn 0!" });
                    }

                    var stock = product.ProductSizes.FirstOrDefault(ps => ps.SizeID == cartItem.SizeID)?.Stock ?? 0;
                    if (quantity.Value > stock)
                    {
                        return Json(new { success = false, message = $"Số lượng vượt quá tồn kho! Chỉ còn {stock} sản phẩm." });
                    }

                    cartItem.Quantity = quantity.Value;
                }

                await _context.SaveChangesAsync();

                var subtotal = cartItem.Product.Price * cartItem.Quantity;
                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == sessionId)
                    .SumAsync(c => c.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật giỏ hàng!",
                    quantity = cartItem.Quantity,
                    subtotal = (double)subtotal, // Đảm bảo kiểu dữ liệu phù hợp
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateCartItem: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra. Vui lòng thử lại sau!" });
            }
        }

        public async Task<int> GetCartCount()
        {
            try
            {
                var sessionId = GetSessionId();
                var cartItems = await _context.Carts
                    .Where(c => c.SessionId == sessionId)
                    .ToListAsync();

                return cartItems.Sum(c => c.Quantity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCartCount: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return 0;
            }
        }
    }
}