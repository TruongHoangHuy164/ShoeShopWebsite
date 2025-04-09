using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
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
                return Json(new { success = false, message = $"Lỗi khi tải dữ liệu dashboard: {ex.Message}" });
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
                TempData["ErrorMessage"] = $"Lỗi khi thêm sản phẩm: {ex.Message}";
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

                if (productToUpdate == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sản phẩm để cập nhật.";
                    return RedirectToAction(nameof(ProductList));
                }

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
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật sản phẩm: {ex.Message}";
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

                if (product == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy sản phẩm để xóa.";
                    return RedirectToAction(nameof(ProductList));
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa sản phẩm thành công!";
                return RedirectToAction(nameof(ProductList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa sản phẩm: {ex.Message}";
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
            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Dashboard));
            }

            try
            {
                order.Status = orderUpdate.Status;
                _context.Update(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật trạng thái đơn hàng thành công!";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật trạng thái đơn hàng: {ex.Message}";
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
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
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
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
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
            if (selectedSizes?.Count != stockQuantities?.Count) return;

            var productSizes = selectedSizes.Select((sizeId, i) => new ProductSize
            {
                ProductID = productId,
                SizeID = sizeId,
                Stock = stockQuantities[i] > 0 ? stockQuantities[i] : 0
            }).Where(ps => ps.Stock > 0).ToList();

            _context.ProductSizes.AddRange(productSizes);
            await _context.SaveChangesAsync();
        }

        private async Task SaveProductColors(int productId, List<int> selectedColors)
        {
            if (selectedColors == null || !selectedColors.Any()) return;

            var productColors = selectedColors.Select(colorId => new ProductColor
            {
                ProductID = productId,
                ColorID = colorId
            }).ToList();

            _context.ProductColors.AddRange(productColors);
            await _context.SaveChangesAsync();
        }

        private async Task SaveProductImages(int productId, List<IFormFile> imageFiles)
        {
            if (imageFiles == null || !imageFiles.Any()) return;

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
            if (deletedImageIds?.Any() == true)
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
                    image.IsPrimary = image.ImageID == primaryImageId;
                }
                await _context.SaveChangesAsync();
            }
        }
        // GET: /Employee/ExportOrderToPdf/{id}
[HttpGet]
    [Route("ExportOrderToPdf/{id}")]
    public async Task<IActionResult> ExportOrderToPdf(int id)
    {
        // Thiết lập giấy phép QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;

        var order = await _context.Orders
            .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Size)
            .Include(o => o.OrderDetails).ThenInclude(od => od.Color)
            .FirstOrDefaultAsync(o => o.OrderID == id);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
            return RedirectToAction(nameof(Dashboard));
        }

        try
        {
            // Tạo PDF với QuestPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30); // 30 points ~ 1 cm
                    page.DefaultTextStyle(x => x.FontSize(12).FontFamily(Fonts.TimesNewRoman));

                    // Header
                    page.Header()
                        .Text("HÓA ĐƠN BÁN HÀNG")
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    page.Content()
                        .PaddingVertical(30)
                        .Column(column =>
                        {
                            // Mã đơn hàng
                            column.Item()
                                .Text($"Mã đơn hàng: #{order.OrderID}")
                                .FontSize(12)
                                .AlignCenter();

                            // Thông tin khách hàng
                            column.Item()
                                .PaddingTop(20)
                                .Text("Thông tin khách hàng")
                                .FontSize(14)
                                .Bold();

                            column.Item()
                                .PaddingTop(5)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(150);
                                        columns.RelativeColumn();
                                    });

                                    table.Cell().Text("Tên khách hàng:").Bold();
                                    table.Cell().Text(order.FullName ?? "Không xác định");

                                    table.Cell().Text("Địa chỉ:").Bold();
                                    table.Cell().Text(order.Address ?? "Không xác định");

                                    table.Cell().Text("Số điện thoại:").Bold();
                                    table.Cell().Text(order.PhoneNumber ?? "Không xác định");

                                    table.Cell().Text("Ngày đặt:").Bold();
                                    table.Cell().Text($"{order.OrderDate:dd/MM/yyyy}");

                                    table.Cell().Text("Phương thức thanh toán:").Bold();
                                    table.Cell().Text(order.PaymentMethod ?? "Không xác định");

                                    table.Cell().Text("Trạng thái:").Bold();
                                    table.Cell().Text(order.Status ?? "Không xác định");
                                });

                            // Chi tiết sản phẩm
                            column.Item()
                                .PaddingTop(20)
                                .Text("Chi tiết sản phẩm")
                                .FontSize(14)
                                .Bold();

                            column.Item()
                                .PaddingTop(5)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(80);
                                        columns.ConstantColumn(80);
                                        columns.ConstantColumn(80);
                                        columns.ConstantColumn(100);
                                    });

                                    // Header bảng
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Tên sản phẩm").Bold();
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Kích cỡ").Bold();
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Màu sắc").Bold();
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Số lượng").Bold();
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Giá").Bold();
                                    });

                                    // Dữ liệu sản phẩm
                                    if (order.OrderDetails != null && order.OrderDetails.Any())
                                    {
                                        foreach (var detail in order.OrderDetails)
                                        {
                                            table.Cell().Padding(5).Text(detail.Product?.ProductName ?? "Không xác định");
                                            table.Cell().Padding(5).Text(detail.Size?.SizeName ?? "Không xác định");
                                            table.Cell().Padding(5).Text(detail.Color?.ColorName ?? "Không xác định");
                                            table.Cell().Padding(5).Text(detail.Quantity.ToString());
                                            table.Cell().Padding(5).Text($"{detail.Price:N0} VNĐ");
                                        }
                                    }
                                    else
                                    {
                                        table.Cell().ColumnSpan(5).Padding(5).Text("Không có sản phẩm trong đơn hàng.");
                                    }
                                });

                            // Tổng tiền
                            column.Item()
                                .PaddingTop(20)
                                .AlignRight()
                                .Text($"Tổng tiền: {order.TotalPrice:N0} VNĐ")
                                .FontSize(14)
                                .Bold();
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            // Tạo file PDF và trả về
            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"HoaDon_{order.OrderID}.pdf");
        }
        catch (Exception ex)
        {
            // Ghi log chi tiết lỗi
            Console.WriteLine($"Lỗi khi tạo PDF: {ex.Message}\nStackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            TempData["ErrorMessage"] = $"Lỗi khi xuất hóa đơn: {ex.Message}";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }
    }
        // GET: /Employee/OrderList
        [HttpGet]
        [Route("OrderList")]
        public async Task<IActionResult> OrderList()
        {
            try
            {
                var orders = await _context.Orders
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new
                    {
                        OrderID = o.OrderID,
                        FullName = o.FullName,
                        TotalPrice = o.TotalPrice,
                        Status = o.Status,
                        OrderDate = o.OrderDate
                    })
                    .ToListAsync();

                return View("~/Views/Employee/OrderList.cshtml", orders);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi tải danh sách đơn hàng: {ex.Message}";
                return RedirectToAction(nameof(Dashboard));
            }
        }
    }
}