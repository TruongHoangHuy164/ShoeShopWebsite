using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        [HttpGet]
        [Route("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .Include(p => p.ProductReviews).ThenInclude(pr => pr.Size)
                .Include(p => p.ProductReviews).ThenInclude(pr => pr.Color)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            ViewData["Reviews"] = product.ProductReviews.ToList();
            return View("~/Views/Product/Details.cshtml", product);
        }

        // GET: Product/AddReview
        [HttpGet]
        [Route("AddReview")]
        [Authorize]
        public async Task<IActionResult> AddReview(int productId, int orderId, int sizeId, int colorId)
        {
            // Kiểm tra đăng nhập (không cần vì [Authorize] đảm bảo)
            var userId = User.Identity.Name;

            // Kiểm tra đơn hàng
            var orderDetail = await _context.OrderDetails
                .Include(od => od.Product)
                .Include(od => od.Size)
                .Include(od => od.Color)
                .Include(od => od.Order)
                .FirstOrDefaultAsync(od => od.OrderID == orderId && od.ProductID == productId
                    && (sizeId == -1 || od.SizeID == sizeId)
                    && (colorId == -1 || od.ColorID == colorId));

            if (orderDetail == null || orderDetail.Order.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Đơn hàng không hợp lệ hoặc chưa hoàn thành.";
                return RedirectToAction("MyOrders");
            }

            // Kiểm tra đánh giá trùng
            if (await _context.ProductReviews.AnyAsync(r => r.ProductID == productId && r.UserID == userId && r.OrderID == orderId))
            {
                TempData["ErrorMessage"] = "Bạn đã đánh giá sản phẩm này.";
                return RedirectToAction("MyOrders");
            }

            // Tạo ViewModel
            var viewModel = new AddReviewViewModel
            {
                ProductName = orderDetail.Product?.ProductName ?? "Không xác định",
                SizeName = orderDetail.Size?.SizeName ?? "Không có",
                ColorName = orderDetail.Color?.ColorName ?? "Không có",
                OrderDate = orderDetail.Order?.OrderDate ?? DateTime.Now,
                Price = orderDetail.Price
            };

            ViewBag.ProductId = productId;
            ViewBag.OrderId = orderId;
            ViewBag.SizeId = sizeId == -1 ? 0 : sizeId;
            ViewBag.ColorId = colorId == -1 ? 0 : colorId;

            return View("~/Views/Product/AddReview.cshtml", viewModel);
        }



        [HttpPost]
        [Route("AddReview")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int orderId, int sizeId, int colorId, AddReviewViewModel model)
        {
            Console.WriteLine($"POST AddReview called at {DateTime.Now:yyyy-MM-dd HH:mm:ss}: productId={productId}, orderId={orderId}, sizeId={sizeId}, colorId={colorId}, Rating={model.Rating}, Comment={model.Comment}");

            try
            {
                if (productId <= 0 || orderId <= 0)
                {
                    Console.WriteLine("Invalid productId or orderId");
                    TempData["ErrorMessage"] = "Thông tin sản phẩm hoặc đơn hàng không hợp lệ.";
                    return RedirectToAction("MyOrders", "Checkout");
                }

                if (!User.Identity.IsAuthenticated)
                {
                    Console.WriteLine("User is not authenticated");
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập để tiếp tục.";
                    return RedirectToAction("Login", "Account");
                }

                var userName = User.Identity.Name;
                if (string.IsNullOrEmpty(userName))
                {
                    Console.WriteLine("UserName is null or empty");
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Account");
                }

                // Truy vấn AspNetUsers để lấy Id dựa trên UserName
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == userName || u.Email == userName);
                if (user == null)
                {
                    Console.WriteLine($"User with UserName or Email {userName} not found in AspNetUsers");
                    TempData["ErrorMessage"] = "Tài khoản người dùng không hợp lệ.";
                    return RedirectToAction("Login", "Account");
                }
                var userId = user.Id; // GUID
                Console.WriteLine($"UserID from AspNetUsers: {userId}");

                var orderDetail = await _context.OrderDetails
                    .Include(od => od.Product)
                    .Include(od => od.Size)
                    .Include(od => od.Color)
                    .Include(od => od.Order)
                    .FirstOrDefaultAsync(od => od.OrderID == orderId && od.ProductID == productId
                        && (sizeId == -1 || od.SizeID == sizeId)
                        && (colorId == -1 || od.ColorID == colorId));

                if (orderDetail == null || orderDetail.Order.Status != "Completed")
                {
                    Console.WriteLine("OrderDetail not found or status not Completed");
                    TempData["ErrorMessage"] = "Đơn hàng không hợp lệ hoặc chưa hoàn thành.";
                    model.ProductName = model.ProductName ?? "Không xác định";
                    model.SizeName = model.SizeName ?? "Không có";
                    model.ColorName = model.ColorName ?? "Không có";
                    model.OrderDate = model.OrderDate;
                    ViewBag.ProductId = productId;
                    ViewBag.OrderId = orderId;
                    ViewBag.SizeId = sizeId;
                    ViewBag.ColorId = colorId;
                    return View("~/Views/Product/AddReview.cshtml", model);
                }

                if (await _context.ProductReviews.AnyAsync(r => r.ProductID == productId && r.UserID == userId && r.OrderID == orderId))
                {
                    Console.WriteLine("Review already exists");
                    TempData["ErrorMessage"] = "Bạn đã đánh giá sản phẩm này.";
                    return RedirectToAction("MyOrders", "Checkout");
                }

                if (!ModelState.IsValid || !model.Rating.HasValue || model.Rating < 1 || model.Rating > 5)
                {
                    Console.WriteLine("Invalid ModelState or Rating");
                    if (!model.Rating.HasValue || model.Rating < 1 || model.Rating > 5)
                    {
                        ModelState.AddModelError("Rating", "Số sao phải từ 1 đến 5.");
                    }
                    TempData["ErrorMessage"] = "Vui lòng kiểm tra thông tin nhập.";
                    model.ProductName = orderDetail.Product?.ProductName ?? model.ProductName;
                    model.SizeName = orderDetail.Size?.SizeName ?? model.SizeName;
                    model.ColorName = orderDetail.Color?.ColorName ?? model.ColorName;
                    model.OrderDate = orderDetail.Order?.OrderDate ?? model.OrderDate;
                    model.Price = orderDetail.Price;
                    ViewBag.ProductId = productId;
                    ViewBag.OrderId = orderId;
                    ViewBag.SizeId = sizeId;
                    ViewBag.ColorId = colorId;
                    return View("~/Views/Product/AddReview.cshtml", model);
                }

                var review = new ProductReview
                {
                    ProductID = productId,
                    OrderID = orderId,
                    UserID = userId,
                    Rating = model.Rating.Value,
                    Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment,
                    ReviewDate = DateTime.Now,
                    SizeID = sizeId == 0 ? null : sizeId,
                    ColorID = colorId == 0 ? null : colorId
                };

                Console.WriteLine($"Review details: ProductID={review.ProductID}, OrderID={review.OrderID}, UserID={review.UserID}, Rating={review.Rating}, SizeID={review.SizeID}, ColorID={review.ColorID}");
                _context.ProductReviews.Add(review);

                Console.WriteLine("Saving changes to database");
                await _context.SaveChangesAsync();
                Console.WriteLine("Review saved successfully");

                TempData["SuccessMessage"] = "Cảm ơn bạn đã đánh giá!";
                return RedirectToAction("MyOrders", "Checkout");
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"DbUpdateException in POST AddReview: {dbEx.Message}\nInnerException: {dbEx.InnerException?.Message}\nStackTrace: {dbEx.StackTrace}");
                TempData["ErrorMessage"] = $"Lỗi khi lưu đánh giá: {dbEx.InnerException?.Message ?? dbEx.Message}";
                return View("~/Views/Product/AddReview.cshtml", model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception in POST AddReview: {ex.Message}\nStackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Đã xảy ra lỗi khi gửi đánh giá.";
                return View("~/Views/Product/AddReview.cshtml", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId, int rating = 0)
        {
            var reviewsQuery = _context.ProductReviews
                .Include(r => r.User)
                .Include(r => r.Size)
                .Include(r => r.Color)
                .Where(r => r.ProductID == productId);

            if (rating > 0)
            {
                reviewsQuery = reviewsQuery.Where(r => r.Rating == rating);
            }

            var reviews = await reviewsQuery
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

            return PartialView("_ReviewsPartial", reviews);
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
    }
}