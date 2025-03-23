using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;

namespace ShoeShop.Controllers
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

        // GET: Product
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .ToListAsync();

            // Debug: Log số lượng màu sắc của mỗi sản phẩm
            foreach (var p in products)
            {
                Console.WriteLine($"Product {p.ProductID}: Colors = {(p.ProductColors != null ? p.ProductColors.Count : 0)}");
                if (p.ProductColors != null && p.ProductColors.Any())
                {
                    foreach (var pc in p.ProductColors)
                    {
                        Console.WriteLine($"  - ColorID: {pc.ColorID}, ColorName: {(pc.Color != null ? pc.Color.ColorName : "null")}");
                    }
                }
            }

            return View(products);
        }

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

            return View(product);
        }

        // GET: Product/Create
        public IActionResult Create()
        {
            ViewData["Sizes"] = _context.Sizes.ToList();
            ViewData["Colors"] = _context.Colors.ToList();
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName");
            return View();
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles)
        {
            try
            {
                // Debug: Log giá trị selectedColors
                Console.WriteLine($"selectedColors: {(selectedColors != null ? string.Join(", ", selectedColors) : "null")}");

                // Kiểm tra dữ liệu đầu vào
                if (product == null) ModelState.AddModelError("", "Dữ liệu sản phẩm không hợp lệ.");
                if (string.IsNullOrEmpty(product.ProductName)) ModelState.AddModelError("ProductName", "Tên sản phẩm là bắt buộc.");
                if (product.CategoryID <= 0) ModelState.AddModelError("CategoryID", "Vui lòng chọn danh mục.");
                if (product.Price <= 0) ModelState.AddModelError("Price", "Giá sản phẩm phải lớn hơn 0.");
                if (selectedColors == null || !selectedColors.Any()) ModelState.AddModelError("selectedColors", "Vui lòng chọn ít nhất một màu sắc.");
                if (selectedSizes == null || !selectedSizes.Any()) ModelState.AddModelError("selectedSizes", "Vui lòng chọn ít nhất một kích cỡ.");
                if (imageFiles == null || !imageFiles.Any()) ModelState.AddModelError("imageFiles", "Vui lòng chọn ít nhất một hình ảnh.");

                // Loại bỏ validation cho ProductColors
                ModelState.Remove("ProductColors");

                if (!ModelState.IsValid)
                {
                    LoadViewData(product);
                    return View(product);
                }

                // Lưu sản phẩm
                product.CreatedAt = DateTime.Now;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Đã lưu sản phẩm ID: {product.ProductID}");

                // Lưu kích thước
                if (selectedSizes != null && stockQuantities != null && selectedSizes.Count == stockQuantities.Count)
                {
                    var productSizes = selectedSizes.Select((sizeId, i) => new ProductSize
                    {
                        ProductID = product.ProductID,
                        SizeID = sizeId,
                        Stock = stockQuantities[i] > 0 ? stockQuantities[i] : 0
                    }).Where(ps => ps.Stock > 0).ToList();
                    _context.ProductSizes.AddRange(productSizes);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ Đã lưu {productSizes.Count} kích thước.");
                }

                // Lưu màu sắc
                if (selectedColors != null && selectedColors.Any())
                {
                    var productColors = selectedColors.Select(colorId => new ProductColor
                    {
                        ProductID = product.ProductID,
                        ColorID = colorId
                    }).ToList();
                    _context.ProductColors.AddRange(productColors);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ Đã lưu {productColors.Count} màu sắc.");
                }

                // Lưu ảnh
                if (imageFiles != null && imageFiles.Any())
                {
                    string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

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
                            ProductID = product.ProductID,
                            ImageURL = "/images/products/" + fileName,
                            IsPrimary = productImages.Count == 0
                        });
                    }
                    _context.ProductImages.AddRange(productImages);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ Đã lưu {productImages.Count} ảnh.");
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi tạo sản phẩm: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
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
                .Include(p => p.ProductColors)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product.CategoryID);
            ViewData["Sizes"] = _context.Sizes.ToList();
            ViewData["Colors"] = _context.Colors.ToList();
            ViewData["ExistingSizes"] = product.ProductSizes.Select(ps => ps.SizeID).ToList();
            ViewData["ExistingColors"] = product.ProductColors.Select(pc => pc.ColorID).ToList();
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
                // Kiểm tra dữ liệu đầu vào
                if (string.IsNullOrEmpty(product.ProductName)) ModelState.AddModelError("ProductName", "Tên sản phẩm là bắt buộc.");
                if (product.CategoryID <= 0) ModelState.AddModelError("CategoryID", "Vui lòng chọn danh mục.");
                if (product.Price <= 0) ModelState.AddModelError("Price", "Giá phải lớn hơn 0.");
                if (selectedSizes == null || !selectedSizes.Any()) ModelState.AddModelError("selectedSizes", "Vui lòng chọn ít nhất một kích cỡ.");
                if (selectedColors == null || !selectedColors.Any()) ModelState.AddModelError("selectedColors", "Vui lòng chọn ít nhất một màu sắc.");

                // Loại bỏ validation cho ProductColors
                ModelState.Remove("ProductColors");

                if (!ModelState.IsValid)
                {
                    LoadViewData(product);
                    var existingProduct = await _context.Products
                        .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                        .Include(p => p.ProductImages)
                        .Include(p => p.ProductColors)
                        .FirstOrDefaultAsync(p => p.ProductID == id);
                    ViewData["ExistingSizes"] = existingProduct.ProductSizes.Select(ps => ps.SizeID).ToList();
                    ViewData["ExistingColors"] = existingProduct.ProductColors.Select(pc => pc.ColorID).ToList();
                    return View(product);
                }

                // Lấy sản phẩm để cập nhật
                var productToUpdate = await _context.Products
                    .Include(p => p.ProductSizes)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == id);

                if (productToUpdate == null) return NotFound();

                // Cập nhật thông tin sản phẩm
                productToUpdate.CategoryID = product.CategoryID;
                productToUpdate.ProductName = product.ProductName;
                productToUpdate.Description = product.Description;
                productToUpdate.Price = product.Price;

                _context.Update(productToUpdate);
                await _context.SaveChangesAsync();

                // Cập nhật kích thước
                if (selectedSizes != null && stockQuantities != null)
                {
                    _context.ProductSizes.RemoveRange(productToUpdate.ProductSizes);
                    await _context.SaveChangesAsync();

                    var productSizes = selectedSizes.Select((sizeId, i) => new ProductSize
                    {
                        ProductID = product.ProductID,
                        SizeID = sizeId,
                        Stock = i < stockQuantities.Count && stockQuantities[i] > 0 ? stockQuantities[i] : 0
                    }).Where(ps => ps.Stock > 0).ToList();
                    _context.ProductSizes.AddRange(productSizes);
                    await _context.SaveChangesAsync();
                }

                // Cập nhật màu sắc
                if (selectedColors != null && selectedColors.Any())
                {
                    _context.ProductColors.RemoveRange(productToUpdate.ProductColors);
                    var productColors = selectedColors.Select(colorId => new ProductColor
                    {
                        ProductID = product.ProductID,
                        ColorID = colorId
                    }).ToList();
                    _context.ProductColors.AddRange(productColors);
                    await _context.SaveChangesAsync();
                }

                // Xử lý ảnh
                if (imageFiles != null && imageFiles.Any())
                {
                    string uploadPath = Path.Combine(_hostEnvironment.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    foreach (var imageFile in imageFiles.Where(f => f != null && f.Length > 0))
                    {
                        string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                        string filePath = Path.Combine(uploadPath, fileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }
                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductID = product.ProductID,
                            ImageURL = "/images/products/" + fileName,
                            IsPrimary = !productToUpdate.ProductImages.Any()
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                // Xóa ảnh
                if (deletedImageIds != null && deletedImageIds.Any())
                {
                    foreach (var imageId in deletedImageIds)
                    {
                        var image = await _context.ProductImages.FindAsync(imageId);
                        if (image != null)
                        {
                            var imagePath = Path.Combine(_hostEnvironment.WebRootPath, image.ImageURL.TrimStart('/'));
                            if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                            _context.ProductImages.Remove(image);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // Cập nhật ảnh chính
                if (primaryImageId.HasValue)
                {
                    var images = await _context.ProductImages.Where(pi => pi.ProductID == product.ProductID).ToListAsync();
                    foreach (var image in images)
                    {
                        image.IsPrimary = (image.ImageID == primaryImageId);
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi cập nhật sản phẩm: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                LoadViewData(product);
                var existingProduct = await _context.Products
                    .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductColors)
                    .FirstOrDefaultAsync(p => p.ProductID == id);
                ViewData["ExistingSizes"] = existingProduct.ProductSizes.Select(ps => ps.SizeID).ToList();
                ViewData["ExistingColors"] = existingProduct.ProductColors.Select(pc => pc.ColorID).ToList();
                return View(existingProduct);
            }
        }

        // Hàm load dữ liệu dropdown
        private void LoadViewData(Product product)
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product?.CategoryID);
            ViewData["Sizes"] = _context.Sizes.ToList();
            ViewData["Colors"] = _context.Colors.ToList();
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

                foreach (var image in product.ProductImages)
                {
                    var imagePath = Path.Combine(_hostEnvironment.WebRootPath, image.ImageURL.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi xóa sản phẩm: {ex.Message}");
                ModelState.AddModelError("", $"Có lỗi xảy ra khi xóa sản phẩm: {ex.Message}");
                return View(await _context.Products.Include(p => p.Category).Include(p => p.ProductImages).Include(p => p.ProductSizes).ThenInclude(ps => ps.Size).FirstOrDefaultAsync(m => m.ProductID == id));
            }
        }

        // POST: Product/SetPrimaryImage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryImage(int imageId, int productId)
        {
            try
            {
                var images = await _context.ProductImages.Where(pi => pi.ProductID == productId).ToListAsync();
                foreach (var image in images) image.IsPrimary = (image.ImageID == imageId);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Edit), new { id = productId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi đặt ảnh chính: {ex.Message}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id = productId });
            }
        }

        // POST: Product/DeleteImage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId, int productId)
        {
            try
            {
                var image = await _context.ProductImages.FindAsync(imageId);
                if (image != null)
                {
                    var imagePath = Path.Combine(_hostEnvironment.WebRootPath, image.ImageURL.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                    _context.ProductImages.Remove(image);
                    await _context.SaveChangesAsync();

                    if (image.IsPrimary)
                    {
                        var newPrimaryImage = await _context.ProductImages.FirstOrDefaultAsync(pi => pi.ProductID == productId);
                        if (newPrimaryImage != null)
                        {
                            newPrimaryImage.IsPrimary = true;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                return RedirectToAction(nameof(Edit), new { id = productId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi xóa ảnh: {ex.Message}");
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id = productId });
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductID == id);
        }
    }
}