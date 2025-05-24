using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class ProductController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductController(NikeShopDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Product/Index
        public async Task<IActionResult> Index(string searchName, string sortPrice, string filterCategory, int page = 1, int pageSize = 20)
        {
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .AsQueryable();

            // Tìm kiếm theo tên
            if (!string.IsNullOrEmpty(searchName))
            {
                productsQuery = productsQuery.Where(p => p.ProductName.ToLower().Contains(searchName.ToLower()));
            }

            // Lọc theo danh mục
            if (!string.IsNullOrEmpty(filterCategory))
            {
                productsQuery = productsQuery.Where(p => p.Category.CategoryName == filterCategory);
            }

            // Sắp xếp theo giá
            switch (sortPrice)
            {
                case "low-to-high":
                    productsQuery = productsQuery.OrderBy(p => p.Price);
                    break;
                case "high-to-low":
                    productsQuery = productsQuery.OrderByDescending(p => p.Price);
                    break;
                default:
                    productsQuery = productsQuery.OrderBy(p => p.ProductID);
                    break;
            }

            // Tính tổng số sản phẩm
            int totalProducts = await productsQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);

            // Lấy sản phẩm cho trang hiện tại
            var products = await productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lưu dữ liệu để hiển thị lại trong view
            ViewData["SearchName"] = searchName;
            ViewData["SortPrice"] = sortPrice;
            ViewData["FilterCategory"] = filterCategory;
            ViewData["Categories"] = await _context.Categories.ToListAsync();
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;

            return View(products);
        }

        // Các action khác giữ nguyên
        // GET: Product/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null) return NotFound();

            var reviews = await _context.ProductReviews
                .Where(r => r.ProductID == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

            ViewData["Reviews"] = reviews;
            ViewData["Sizes"] = product.ProductSizes.Select(ps => ps.Size).ToList();
            ViewData["Colors"] = product.ProductColors.Select(pc => pc.Color).ToList();

            return View(product);
        }

        // GET: Product/Wishlist
        public async Task<IActionResult> Wishlist()
        {
            var userId = User.Identity.Name ?? "Guest";
            var wishlistItems = await _context.Wishlist
                .Where(w => w.UserID == userId)
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .Select(w => w.Product)
                .ToListAsync();

            return View(wishlistItems);
        }

        // GET: Product/GetWishlistCount
        [HttpGet]
        public async Task<IActionResult> GetWishlistCount()
        {
            var userId = User.Identity.Name ?? "Guest";
            var count = await _context.Wishlist
                .CountAsync(w => w.UserID == userId);
            return Json(count);
        }

        // POST: Product/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int selectedSizeId, int selectedColorId, int quantity = 1)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == productId);

                if (product == null)
                    return Json(new { success = false, message = "Sản phẩm không tồn tại!" });

                var productSize = product.ProductSizes?.FirstOrDefault(ps => ps.SizeID == selectedSizeId);
                var productColor = product.ProductColors?.FirstOrDefault(pc => pc.ColorID == selectedColorId);

                if (productSize == null || productColor == null)
                    return Json(new { success = false, message = "Vui lòng chọn kích cỡ và màu sắc hợp lệ!" });

                if (quantity <= 0 || quantity > productSize.Stock)
                    return Json(new { success = false, message = "Số lượng không hợp lệ hoặc vượt quá tồn kho!" });

                var cartItem = new
                {
                    ProductId = productId,
                    SizeId = selectedSizeId,
                    ColorId = selectedColorId,
                    Quantity = quantity,
                    Price = product.Price
                };

                return Json(new { success = true, message = "Đã thêm sản phẩm vào giỏ hàng!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi thêm vào giỏ hàng: {ex.Message}");
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        // POST: Product/ToggleWishlist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            try
            {
                var userId = User.Identity.Name ?? "Guest";
                var wishlistItem = await _context.Wishlist
                    .FirstOrDefaultAsync(w => w.UserID == userId && w.ProductID == productId);

                if (wishlistItem == null)
                {
                    _context.Wishlist.Add(new Wishlist { UserID = userId, ProductID = productId });
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Đã thêm vào danh sách yêu thích!" });
                }
                else
                {
                    _context.Wishlist.Remove(wishlistItem);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Đã xóa khỏi danh sách yêu thích!" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi toggle wishlist: {ex.Message}");
                return Json(new { success = false, message = "Đã có lỗi xảy ra!" });
            }
        }

        // GET: Product/Create
        public IActionResult Create()
        {
            LoadViewData(null);
            return View();
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles)
        {
            try
            {
                if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, imageFiles))
                {
                    LoadViewData(product);
                    return View(product);
                }

                product.CreatedAt = DateTime.Now;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                await SaveProductSizes(product.ProductID, selectedSizes, stockQuantities);
                await SaveProductColors(product.ProductID, selectedColors);
                await SaveProductImages(product.ProductID, imageFiles);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi tạo sản phẩm: {ex.Message}");
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                LoadViewData(product);
                return View(product);
            }
        }

        // GET: Product/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            LoadViewData(product);
            ViewData["ExistingSizes"] = product.ProductSizes.Select(ps => ps.SizeID).ToList();
            ViewData["ExistingColors"] = product.ProductColors.Select(pc => pc.ColorID).ToList();
            ViewData["StockQuantities"] = product.ProductSizes.ToDictionary(ps => ps.SizeID, ps => ps.Stock);

            return View(product);
        }

        // POST: Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, List<int> selectedSizes, List<int> selectedColors,
            List<int> stockQuantities, List<IFormFile> imageFiles, List<int> deletedImageIds, int? primaryImageId)
        {
            if (id != product.ProductID) return NotFound();

            try
            {
                if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, null))
                {
                    LoadViewData(product);
                    await LoadExistingData(id);
                    return View(product);
                }

                var productToUpdate = await _context.Products
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == id);

                if (productToUpdate == null) return NotFound();

                UpdateProduct(productToUpdate, product);
                await _context.SaveChangesAsync();

                await UpdateProductSizes(productToUpdate.ProductID, selectedSizes, stockQuantities);
                await UpdateProductColors(productToUpdate.ProductID, selectedColors);
                await UpdateProductImages(productToUpdate.ProductID, imageFiles, deletedImageIds, primaryImageId);

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi cập nhật sản phẩm: {ex.Message}");
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                LoadViewData(product);
                await LoadExistingData(id);
                return View(product);
            }
        }

        // GET: Product/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .FirstOrDefaultAsync(m => m.ProductID == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // POST: Product/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == id);

                if (product == null) return NotFound();

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi xóa sản phẩm: {ex.Message}");
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                return View(await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(m => m.ProductID == id));
            }
        }

        // Helper Methods
        private void LoadViewData(Product product)
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product?.CategoryID);
            ViewData["Sizes"] = _context.Sizes.ToList();
            ViewData["Colors"] = _context.Colors.ToList();
        }

        private async Task LoadExistingData(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors)
                .FirstOrDefaultAsync(p => p.ProductID == id);
            ViewData["ExistingSizes"] = product?.ProductSizes.Select(ps => ps.SizeID).ToList() ?? new List<int>();
            ViewData["ExistingColors"] = product?.ProductColors.Select(pc => pc.ColorID).ToList() ?? new List<int>();
            ViewData["StockQuantities"] = product?.ProductSizes.ToDictionary(ps => ps.SizeID, ps => ps.Stock) ?? new Dictionary<int, int>();
        }

        private bool ValidateProductInput(Product product, List<int> selectedSizes, List<int> selectedColors, List<IFormFile> imageFiles)
        {
            if (string.IsNullOrEmpty(product.ProductName)) ModelState.AddModelError("ProductName", "Tên sản phẩm là bắt buộc.");
            if (product.CategoryID <= 0) ModelState.AddModelError("CategoryID", "Vui lòng chọn danh mục.");
            if (product.Price <= 0) ModelState.AddModelError("Price", "Giá sản phẩm phải lớn hơn 0.");
            if (selectedSizes == null || !selectedSizes.Any()) ModelState.AddModelError("selectedSizes", "Vui lòng chọn ít nhất một kích cỡ.");
            if (selectedColors == null || !selectedColors.Any()) ModelState.AddModelError("selectedColors", "Vui lòng chọn ít nhất một màu sắc.");
            if (imageFiles != null && !imageFiles.Any() && !_context.ProductImages.Any(pi => pi.ProductID == product.ProductID))
                ModelState.AddModelError("imageFiles", "Vui lòng chọn ít nhất một hình ảnh.");

            return ModelState.IsValid;
        }

        private async Task SaveProductSizes(int productId, List<int> selectedSizes, List<int> stockQuantities)
        {
            if (selectedSizes != null && stockQuantities != null && selectedSizes.Count == stockQuantities.Count)
            {
                var productSizes = selectedSizes.Select((sizeId, i) => new ProductSize
                {
                    ProductID = productId,
                    SizeID = sizeId,
                    Stock = stockQuantities[i] > 0 ? stockQuantities[i] : 0
                }).Where(ps => ps.Stock > 0).ToList();
                _context.ProductSizes.AddRange(productSizes);
                await _context.SaveChangesAsync();
            }
        }

        private async Task SaveProductColors(int productId, List<int> selectedColors)
        {
            if (selectedColors != null && selectedColors.Any())
            {
                var productColors = selectedColors.Select(colorId => new ProductColor
                {
                    ProductID = productId,
                    ColorID = colorId
                }).ToList();
                _context.ProductColors.AddRange(productColors);
                await _context.SaveChangesAsync();
            }
        }

        private async Task SaveProductImages(int productId, List<IFormFile> imageFiles)
        {
            if (imageFiles != null && imageFiles.Any())
            {
                string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                Directory.CreateDirectory(uploadPath);

                var productImages = new List<ProductImage>();
                foreach (var imageFile in imageFiles.Where(f => f != null && f.Length > 0))
                {
                    string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                    string filePath = Path.Combine(uploadPath, fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }
                    productImages.Add(new ProductImage
                    {
                        ProductID = productId,
                        ImageURL = "/images/products/" + fileName,
                        IsPrimary = productImages.Count == 0
                    });
                }
                _context.ProductImages.AddRange(productImages);
                await _context.SaveChangesAsync();
            }
        }

        private void UpdateProduct(Product productToUpdate, Product product)
        {
            productToUpdate.CategoryID = product.CategoryID;
            productToUpdate.ProductName = product.ProductName;
            productToUpdate.Description = product.Description;
            productToUpdate.Price = product.Price;
            _context.Update(productToUpdate);
        }

        private async Task UpdateProductSizes(int productId, List<int> selectedSizes, List<int> stockQuantities)
        {
            var existingSizes = await _context.ProductSizes.Where(ps => ps.ProductID == productId).ToListAsync();
            _context.ProductSizes.RemoveRange(existingSizes);
            await _context.SaveChangesAsync();

            await SaveProductSizes(productId, selectedSizes, stockQuantities);
        }

        private async Task UpdateProductColors(int productId, List<int> selectedColors)
        {
            var existingColors = await _context.ProductColors.Where(pc => pc.ProductID == productId).ToListAsync();
            _context.ProductColors.RemoveRange(existingColors);
            await _context.SaveChangesAsync();

            await SaveProductColors(productId, selectedColors);
        }

        private async Task UpdateProductImages(int productId, List<IFormFile> imageFiles, List<int> deletedImageIds, int? primaryImageId)
        {
            if (deletedImageIds != null && deletedImageIds.Any())
            {
                var imagesToDelete = await _context.ProductImages
                    .Where(pi => pi.ProductID == productId && deletedImageIds.Contains(pi.ImageID))
                    .ToListAsync();
                _context.ProductImages.RemoveRange(imagesToDelete);
                await _context.SaveChangesAsync();
            }

            await SaveProductImages(productId, imageFiles);

            if (primaryImageId.HasValue)
            {
                var images = await _context.ProductImages.Where(pi => pi.ProductID == productId).ToListAsync();
                foreach (var image in images)
                {
                    image.IsPrimary = (image.ImageID == primaryImageId);
                }
                await _context.SaveChangesAsync();
            }
        }
        // GET: Product/AddReview/5?orderId=1
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> AddReview(int productId)
        {
            var userId = User.Identity.Name;
            var hasPurchased = await _context.OrderDetails
                .Join(_context.Orders,
                    od => od.OrderID,
                    o => o.OrderID,
                    (od, o) => new { od.ProductID, o.SessionId, o.Status })
                .AnyAsync(x => x.ProductID == productId && x.SessionId == userId && x.Status == "Completed");

            if (!hasPurchased)
            {
                TempData["ErrorMessage"] = "Bạn chỉ có thể đánh giá sản phẩm đã mua và hoàn tất đơn hàng.";
                return RedirectToAction("Index");
            }

            var hasReviewed = await _context.ProductReviews
                .AnyAsync(r => r.ProductID == productId && r.UserID == userId);

            if (hasReviewed)
            {
                TempData["ErrorMessage"] = "Bạn đã đánh giá sản phẩm này.";
                return RedirectToAction("Index");
            }

            var model = new ProductReview
            {
                ProductID = productId,
                UserID = userId,
                ReviewDate = DateTime.Now
            };

            ViewData["ProductName"] = (await _context.Products.FindAsync(productId))?.ProductName;
            return View("~/Views/Product/AddReview.cshtml", model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(ProductReview review)
        {
            if (!ModelState.IsValid || review.Rating < 1 || review.Rating > 5)
            {
                ModelState.AddModelError("Rating", "Điểm đánh giá phải từ 1 đến 5.");
                ViewData["ProductName"] = (await _context.Products.FindAsync(review.ProductID))?.ProductName;
                return View("~/Views/Product/AddReview.cshtml", review);
            }

            try
            {
                review.UserID = User.Identity.Name;
                review.ReviewDate = DateTime.Now;
                _context.ProductReviews.Add(review);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Thêm đánh giá thành công!";
                return RedirectToAction("Details", new { id = review.ProductID });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm đánh giá: {ex.Message}";
                ViewData["ProductName"] = (await _context.Products.FindAsync(review.ProductID))?.ProductName;
                return View("~/Views/Product/AddReview.cshtml", review);
            }
        }
    }
}