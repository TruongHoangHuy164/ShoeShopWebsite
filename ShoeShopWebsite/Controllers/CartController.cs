using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string SessionIdKey = "CartSessionId";

        public CartController(NikeShopDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        private string GetSessionId()
        {
            if (_httpContextAccessor.HttpContext?.Session == null)
            {
                Console.WriteLine("Session is not available, generating temporary ID");
                return Guid.NewGuid().ToString();
            }

            var sessionId = _httpContextAccessor.HttpContext.Session.GetString(SessionIdKey);
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                _httpContextAccessor.HttpContext.Session.SetString(SessionIdKey, sessionId);
                Console.WriteLine($"Generated new SessionId: {sessionId}");
            }
            return sessionId;
        }

        private string GetCartIdentifier()
        {
            var sessionId = GetSessionId();
            Console.WriteLine($"CartIdentifier determined: {sessionId}");
            return sessionId;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int sizeId, int? colorId = null, int quantity = 1)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Chưa đăng nhập, vui lòng đăng nhập!" });
                }

                if (!User.IsInRole(SD.Role_Customer))
                {
                    return Json(new { success = false, message = "Bạn không có quyền truy cập chức năng này!" });
                }

                Console.WriteLine($"AddToCart started - ProductID: {productId}, SizeID: {sizeId}, ColorID: {colorId}, Quantity: {quantity}");

                var product = await _context.Products
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductColors)
                        .ThenInclude(pc => pc.Color)
                    .FirstOrDefaultAsync(p => p.ProductID == productId);

                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });
                }

                if (product.ProductSizes == null || !product.ProductSizes.Any(ps => ps.SizeID == sizeId))
                {
                    return Json(new { success = false, message = "Kích thước không hợp lệ!" });
                }

                if (colorId.HasValue && (product.ProductColors == null || !product.ProductColors.Any(pc => pc.ColorID == colorId.Value)))
                {
                    return Json(new { success = false, message = "Màu sắc không hợp lệ!" });
                }

                var stock = product.ProductSizes.First(ps => ps.SizeID == sizeId).Stock;
                if (quantity > stock)
                {
                    return Json(new { success = false, message = $"Số lượng vượt quá tồn kho! Chỉ còn {stock} sản phẩm." });
                }

                var cartIdentifier = GetCartIdentifier();

                var cartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.ProductID == productId &&
                                             c.SizeID == sizeId &&
                                             c.ColorID == colorId &&
                                             c.SessionId == cartIdentifier);

                if (cartItem == null)
                {
                    cartItem = new Cart
                    {
                        ProductID = productId,
                        SizeID = sizeId,
                        ColorID = colorId,
                        Quantity = quantity,
                        SessionId = cartIdentifier
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
                Console.WriteLine("Database save successful");

                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == cartIdentifier)
                    .SumAsync(c => c.Quantity);

                return Json(new { success = true, message = "Sản phẩm đã được thêm vào giỏ hàng!", cartCount = cartCount });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddToCart: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra. Vui lòng thử lại sau!" });
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Chưa đăng nhập, vui lòng đăng nhập!" });
                }

                if (!User.IsInRole(SD.Role_Customer))
                {
                    return Json(new { success = false, message = "Bạn không có quyền truy cập chức năng này!" });
                }

                var cartIdentifier = GetCartIdentifier();
                Console.WriteLine($"Index - CartIdentifier: {cartIdentifier}");

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
                    .Where(c => c.SessionId == cartIdentifier)
                    .ToListAsync();

                Console.WriteLine($"Index - Number of cart items: {cartItems.Count}");
                return View(cartItems);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Index: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra. Vui lòng thử lại sau!" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartId)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Chưa đăng nhập, vui lòng đăng nhập!" });
                }

                if (!User.IsInRole(SD.Role_Customer))
                {
                    return Json(new { success = false, message = "Bạn không có quyền truy cập chức năng này!" });
                }

                var cartIdentifier = GetCartIdentifier();
                var cartItem = await _context.Carts
                    .FirstOrDefaultAsync(c => c.CartID == cartId && c.SessionId == cartIdentifier);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng!" });
                }

                _context.Carts.Remove(cartItem);
                await _context.SaveChangesAsync();

                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == cartIdentifier)
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
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Chưa đăng nhập, vui lòng đăng nhập!" });
                }

                if (!User.IsInRole(SD.Role_Customer))
                {
                    return Json(new { success = false, message = "Bạn không có quyền truy cập chức năng này!" });
                }

                var cartIdentifier = GetCartIdentifier();
                Console.WriteLine($"UpdateCartItem - CartIdentifier: {cartIdentifier}, CartId: {cartId}, SizeId: {sizeId}, ColorId: {colorId}, Quantity: {quantity}");

                var cartItem = await _context.Carts
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductSizes)
                    .Include(c => c.Product)
                        .ThenInclude(p => p.ProductColors)
                    .FirstOrDefaultAsync(c => c.CartID == cartId && c.SessionId == cartIdentifier);

                if (cartItem == null)
                {
                    Console.WriteLine("Cart item not found");
                    return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng!" });
                }

                var product = cartItem.Product;

                if (sizeId.HasValue)
                {
                    var sizeExists = product.ProductSizes.Any(ps => ps.SizeID == sizeId.Value);
                    if (!sizeExists)
                    {
                        return Json(new { success = false, message = "Kích thước không hợp lệ!" });
                    }
                    cartItem.SizeID = sizeId.Value;
                }

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
                    .Where(c => c.SessionId == cartIdentifier)
                    .SumAsync(c => c.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật giỏ hàng!",
                    quantity = cartItem.Quantity,
                    subtotal = (double)subtotal,
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
                if (!User.Identity.IsAuthenticated)
                {
                    return 0; // Trả về 0 nếu chưa đăng nhập
                }

                var cartIdentifier = GetCartIdentifier();
                var cartCount = await _context.Carts
                    .Where(c => c.SessionId == cartIdentifier)
                    .SumAsync(c => c.Quantity);
                return cartCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetCartCount: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return 0;
            }
        }
    }
}