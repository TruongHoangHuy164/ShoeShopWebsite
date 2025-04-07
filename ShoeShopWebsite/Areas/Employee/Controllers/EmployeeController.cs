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
    [Authorize(Roles = "Employee")]
    [Route("Employee")]
    public class EmployeeController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public EmployeeController(NikeShopDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: /Employee/Dashboard
        [HttpGet]
        [Route("Dashboard")]
        public IActionResult Dashboard()
        {
            return View("~/Views/Employee/Dashboard.cshtml");
        }

        // GET: /Employee/GetDashboardData
        [HttpGet]
        [Route("GetDashboardData")]
        public IActionResult GetDashboardData()
        {
            try
            {
                var pendingOrders = _context.Orders.Count(o => o.Status == "Pending");
                var deliveringOrders = _context.Orders.Count(o => o.Status == "Delivering");

                var lowStock = _context.ProductSizes
                    .Where(ps => ps.Stock < 20)
                    .GroupBy(ps => ps.ProductID)
                    .Select(g => new
                    {
                        ProductID = g.Key,
                        ProductName = g.First().Product.ProductName,
                        Sizes = g.Select(ps => new { SizeName = ps.Size.SizeName, Stock = ps.Stock }).ToList()
                    })
                    .ToList();

                var recentOrders = _context.Orders
                    .OrderByDescending(o => o.OrderDate)
                    .Take(5)
                    .Select(o => new
                    {
                        orderID = o.OrderID,
                        fullName = o.FullName,
                        totalPrice = o.TotalPrice,
                        status = o.Status
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    pendingOrders,
                    deliveringOrders,
                    lowStockProducts = lowStock.Count,
                    lowStock,
                    recentOrders
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Lỗi khi lấy dữ liệu dashboard: {ex.Message}");
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tải dữ liệu!" });
            }
        }

        // GET: /Employee/ProductList
        [HttpGet]
        [Route("ProductList")]
        public IActionResult ProductList()
        {
            var products = _context.Products
                .Select(p => new
                {
                    ProductID = p.ProductID,
                    ProductName = p.ProductName,
                    Price = p.Price,
                    Sizes = _context.ProductSizes
                        .Where(ps => ps.ProductID == p.ProductID)
                        .Select(ps => new { SizeName = ps.Size.SizeName, Stock = ps.Stock })
                        .ToList()
                })
                .ToList();

            return View("~/Views/Employee/ProductList.cshtml", products);
        }

        // GET: /Employee/ProductDetails/{id}
        [HttpGet]
        [Route("ProductDetails/{id}")]
        public async Task<IActionResult> ProductDetails(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            ViewData["Sizes"] = product.ProductSizes.Select(ps => ps.Size).ToList();
            ViewData["Colors"] = product.ProductColors.Select(pc => pc.Color).ToList();

            return View("~/Views/Employee/ProductDetails.cshtml", product);
        }

        // GET: /Employee/CreateProduct
        [HttpGet]
        [Route("CreateProduct")]
        public IActionResult CreateProduct()
        {
            LoadViewData(null);
            return View("~/Views/Employee/CreateProduct.cshtml");
        }

        // POST: /Employee/CreateProduct
        [HttpPost]
        [Route("CreateProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles)
        {
            if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, imageFiles))
            {
                LoadViewData(product);
                return View("~/Views/Employee/CreateProduct.cshtml", product);
            }

            try
            {
                product.CreatedAt = DateTime.Now;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                await SaveProductSizes(product.ProductID, selectedSizes, stockQuantities);
                await SaveProductColors(product.ProductID, selectedColors);
                await SaveProductImages(product.ProductID, imageFiles);

                TempData["SuccessMessage"] = "Thêm sản phẩm thành công!";
                return RedirectToAction(nameof(ProductList));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                LoadViewData(product);
                return View("~/Views/Employee/CreateProduct.cshtml", product);
            }
        }

        // GET: /Employee/EditProduct/{id}
        [HttpGet]
        [Route("EditProduct/{id}")]
        public async Task<IActionResult> EditProduct(int? id)
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

            return View("~/Views/Employee/EditProduct.cshtml", product);
        }

        // POST: /Employee/EditProduct/{id}
        [HttpPost]
        [Route("EditProduct/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles, List<int> deletedImageIds, int? primaryImageId)
        {
            if (id != product.ProductID) return NotFound();

            if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, null))
            {
                LoadViewData(product);
                await LoadExistingData(id);
                return View("~/Views/Employee/EditProduct.cshtml", product);
            }

            try
            {
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

                TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
                return RedirectToAction(nameof(ProductList));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Có lỗi xảy ra: {ex.Message}");
                LoadViewData(product);
                await LoadExistingData(id);
                return View("~/Views/Employee/EditProduct.cshtml", product);
            }
        }

        // GET: /Employee/DeleteProduct/{id}
        [HttpGet]
        [Route("DeleteProduct/{id}")]
        public async Task<IActionResult> DeleteProduct(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            return View("~/Views/Employee/DeleteProduct.cshtml", product);
        }

        // POST: /Employee/DeleteProduct/{id}
        [HttpPost]
        [Route("DeleteProduct/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
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

                TempData["SuccessMessage"] = "Xóa sản phẩm thành công!";
                return RedirectToAction(nameof(ProductList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction(nameof(ProductList));
            }
        }

        // GET: /Employee/OrderDetails/{id}
        [HttpGet]
        [Route("OrderDetails/{id}")]
        public IActionResult OrderDetails(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Size)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Color)
                .FirstOrDefault(o => o.OrderID == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Dashboard));
            }

            return View("~/Views/Employee/OrderDetails.cshtml", order);
        }

        // GET: /Employee/UpdateOrderStatus/{id}
        [HttpGet]
        [Route("UpdateOrderStatus/{id}")]
        public IActionResult UpdateOrderStatus(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.OrderID == id);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Dashboard));
            }

            ViewBag.Statuses = new[] { "Pending", "Confirmed", "Delivering", "Completed" };
            return View("~/Views/Employee/UpdateOrderStatus.cshtml", order);
        }

        // POST: /Employee/UpdateOrderStatus/{id}
        [HttpPost]
        [Route("UpdateOrderStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int id, [Bind("OrderID,Status")] Order orderUpdate)
        {
            if (id != orderUpdate.OrderID) return NotFound();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
            if (order == null) return NotFound();

            try
            {
                order.Status = orderUpdate.Status;
                _context.Update(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật trạng thái đơn hàng thành công!";
                return RedirectToAction(nameof(OrderDetails), new { id = order.OrderID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Đã xảy ra lỗi khi cập nhật trạng thái.");
                ViewBag.Statuses = new[] { "Pending", "Confirmed", "Delivering", "Completed" };
                return View("~/Views/Employee/UpdateOrderStatus.cshtml", order);
            }
        }

        // POST: /Employee/ConfirmOrder/{id}
        [HttpPost]
        [Route("ConfirmOrder/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
                if (order == null || order.Status != "Pending")
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng hoặc trạng thái không hợp lệ!" });
                }

                order.Status = "Confirmed";
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đơn hàng đã được xác nhận!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Đã xảy ra lỗi: {ex.Message}" });
            }
        }

        // POST: /Employee/DeliverOrder/{id}
        [HttpPost]
        [Route("DeliverOrder/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeliverOrder(int id)
        {
            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
                if (order == null || order.Status != "Confirmed")
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng hoặc trạng thái không hợp lệ!" });
                }

                order.Status = "Delivering";
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Đơn hàng đang được giao!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Đã xảy ra lỗi: {ex.Message}" });
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