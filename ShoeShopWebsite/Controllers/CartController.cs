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
            }
            return sessionId;
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int sizeId, int quantity = 1)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            var size = await _context.Sizes.FirstOrDefaultAsync(s => s.SizeID == sizeId);

            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại!" });
            }

            if (size == null)
            {
                return Json(new { success = false, message = "Kích thước không tồn tại!" });
            }

            var sessionId = GetSessionId();

            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.ProductID == productId && c.SizeID == sizeId && c.SessionId == sessionId);

            if (cartItem == null)
            {
                cartItem = new Cart
                {
                    ProductID = productId,
                    SizeID = sizeId,
                    Quantity = quantity,
                    SessionId = sessionId
                };
                _context.Carts.Add(cartItem);
            }
            else
            {
                cartItem.Quantity += quantity;
            }

            await _context.SaveChangesAsync();

            // Lấy số lượng giỏ hàng mới để trả về
            var cartCount = await _context.Carts
                .Where(c => c.SessionId == sessionId)
                .SumAsync(c => c.Quantity);

            return Json(new { success = true, message = "Sản phẩm đã được thêm vào giỏ hàng!", cartCount = cartCount });
        }

        public async Task<IActionResult> Index()
        {
            var sessionId = GetSessionId();

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(c => c.Size)
                .Where(c => c.SessionId == sessionId)
                .ToListAsync();

            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int cartId)
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

            // Lấy số lượng giỏ hàng mới để trả về
            var cartCount = await _context.Carts
                .Where(c => c.SessionId == sessionId)
                .SumAsync(c => c.Quantity);

            return Json(new { success = true, message = "Sản phẩm đã được xóa khỏi giỏ hàng!", cartCount = cartCount });
        }

        public async Task<int> GetCartCount()
        {
            var sessionId = GetSessionId();
            var cartItems = await _context.Carts
                .Where(c => c.SessionId == sessionId)
                .ToListAsync();

            return cartItems.Sum(c => c.Quantity);
        }
    }
}