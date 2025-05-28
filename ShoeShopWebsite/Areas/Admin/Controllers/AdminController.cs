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

namespace ShoeShopWebsite.Areas.Admin.Controllers
{
    [Authorize(Roles = SD.Role_Admin)]
    [Route("Admin")]
    public class AdminController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            NikeShopDbContext context,
            IWebHostEnvironment hostEnvironment,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        }

        [HttpGet]
        [Route("AdminDashboard")]
        public async Task<IActionResult> AdminDashboard()
        {
            var stats = new AdminDashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                TotalProducts = await _context.Products.CountAsync(),
                TotalOrders = await _context.Orders.CountAsync(),
                TotalRevenue = await _context.Orders.Where(o => o.Status == "Completed").SumAsync(o => o.TotalPrice),
                MonthlyRevenue = await GetMonthlyRevenue(),
                QuarterlyRevenue = await GetQuarterlyRevenue(),
                CategoryRevenueData = await GetCategoryRevenue(),
                BestSellingProducts = await GetBestSellingProducts(),
                DiscountCodeCount = await _context.DiscountCodes.CountAsync(),
                TotalReviews = await _context.ProductReviews.CountAsync() // Thêm mới
            };
            return View("~/Views/Admin/AdminDashboard.cshtml", stats);
        }

        private async Task<List<object>> GetMonthlyRevenue()
        {
            var monthlyRevenue = await _context.Orders
                .Where(o => o.Status == "Completed" && o.OrderDate.Year == 2025)
                .GroupBy(o => o.OrderDate.Month)
                .Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.TotalPrice) })
                .OrderBy(g => g.Month)
                .Take(5)
                .ToListAsync();

            var data = new List<object>();
            for (int i = 0; i < 5; i++)
            {
                var revenue = monthlyRevenue.FirstOrDefault(m => m.Month == i + 1)?.Revenue ?? 0;
                data.Add(revenue);
            }
            return data;
        }

        private async Task<List<object>> GetQuarterlyRevenue()
        {
            var quarterlyRevenue = await _context.Orders
                .Where(o => o.Status == "Completed" && o.OrderDate.Year == 2025)
                .GroupBy(o => (o.OrderDate.Month - 1) / 3 + 1)
                .Select(g => new { Quarter = g.Key, Revenue = g.Sum(o => o.TotalPrice) })
                .OrderBy(g => g.Quarter)
                .ToListAsync();

            var data = new List<object>();
            for (int i = 1; i <= 4; i++)
            {
                var revenue = quarterlyRevenue.FirstOrDefault(q => q.Quarter == i)?.Revenue ?? 0;
                data.Add(revenue);
            }
            return data;
        }

        private async Task<List<BestSellingProduct>> GetBestSellingProducts()
        {
            var bestSelling = await _context.OrderDetails
                .Join(_context.Orders,
                    od => od.OrderID,
                    o => o.OrderID,
                    (od, o) => new { OrderDetail = od, Order = o })
                .Where(o => o.Order.Status == "Completed")
                .GroupBy(od => od.OrderDetail.ProductID)
                .Select(g => new BestSellingProduct
                {
                    ProductID = g.Key,
                    ProductName = _context.Products.FirstOrDefault(p => p.ProductID == g.Key).ProductName,
                    TotalQuantitySold = g.Sum(od => od.OrderDetail.Quantity),
                    TotalRevenue = g.Sum(od => od.OrderDetail.Price * od.OrderDetail.Quantity)
                })
                .OrderByDescending(p => p.TotalQuantitySold)
                .Take(5)
                .ToListAsync();

            return bestSelling;
        }

        private async Task<(List<string> CategoryNames, List<object> Revenues)> GetCategoryRevenue()
        {
            var categories = await _context.Categories
                .Select(c => c.CategoryName)
                .ToListAsync();

            var categoryRevenue = await _context.Orders
                .Where(o => o.Status == "Completed")
                .Join(_context.OrderDetails,
                    o => o.OrderID,
                    od => od.OrderID,
                    (o, od) => new { Order = o, OrderDetail = od })
                .Join(_context.Products,
                    ood => ood.OrderDetail.ProductID,
                    p => p.ProductID,
                    (ood, p) => new { ood.Order, ood.OrderDetail, Product = p })
                .Join(_context.Categories,
                    opod => opod.Product.CategoryID,
                    c => c.CategoryID,
                    (opod, c) => new { opod.Order, opod.OrderDetail, opod.Product, Category = c })
                .GroupBy(x => x.Category.CategoryName)
                .Select(g => new { CategoryName = g.Key, Revenue = g.Sum(x => x.OrderDetail.Price * x.OrderDetail.Quantity) })
                .ToListAsync();

            var revenues = new List<object>();
            foreach (var cat in categories)
            {
                var revenue = categoryRevenue.FirstOrDefault(c => c.CategoryName == cat)?.Revenue ?? 0;
                revenues.Add(revenue);
            }

            return (categories, revenues);
        }

        [HttpGet]
        [Route("ReviewList")]
        public async Task<IActionResult> ReviewList(string search, int page = 1, int pageSize = 10)
        {
            var query = _context.ProductReviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .Include(r => r.Size)
                .Include(r => r.Color)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(r =>
                    r.Product.ProductName.Contains(search) ||
                    (r.User.FullName != null && r.User.FullName.Contains(search)) ||
                    r.User.UserName.Contains(search) ||
                    r.Comment.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.ReviewDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewViewModel
                {
                    ReviewID = r.ReviewID,
                    ProductName = r.Product.ProductName,
                    UserName = r.User.FullName ?? r.User.UserName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    ReviewDate = r.ReviewDate,
                    Size = r.Size != null ? r.Size.SizeName : "N/A",
                    Color = r.Color != null ? r.Color.ColorName : "N/A"
                })
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;

            return View("~/Views/Admin/ReviewList.cshtml", reviews);
        }

        [HttpPost]
        [Route("DeleteReview/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var review = await _context.ProductReviews.FirstOrDefaultAsync(r => r.ReviewID == id);
                if (review == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đánh giá để xóa.";
                    return RedirectToAction(nameof(ReviewList));
                }

                _context.ProductReviews.Remove(review);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa đánh giá thành công!";
                return RedirectToAction(nameof(ReviewList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa đánh giá: {ex.Message}";
                return RedirectToAction(nameof(ReviewList));
            }
        }

        // Các phương thức khác giữ nguyên
        [HttpGet]
        [Route("UserList")]
        public async Task<IActionResult> UserList()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<(ApplicationUser User, IList<string> Roles)>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add((user, roles));
            }
            return View("~/Views/Admin/UserList.cshtml", userList);
        }

        [HttpGet]
        [Route("CreateUser")]
        public IActionResult CreateUser()
        {
            ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name");
            return View("~/Views/Admin/CreateUser.cshtml");
        }

        [HttpPost]
        [Route("CreateUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(ApplicationUser user, string password, string selectedRole)
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(selectedRole))
            {
                ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                return View("~/Views/Admin/CreateUser.cshtml", user);
            }

            try
            {
                user.UserName = user.Email;
                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                    return View("~/Views/Admin/CreateUser.cshtml", user);
                }

                await _userManager.AddToRoleAsync(user, selectedRole);
                TempData["SuccessMessage"] = "Thêm người dùng thành công!";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm người dùng: {ex.Message}";
                ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                return View("~/Views/Admin/CreateUser.cshtml", user);
            }
        }

        [HttpPost]
        [Route("LockUser/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || await _userManager.IsInRoleAsync(user, SD.Role_Admin))
            {
                TempData["ErrorMessage"] = "Không thể khóa tài khoản Admin hoặc người dùng không tồn tại.";
                return RedirectToAction(nameof(UserList));
            }

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            TempData["SuccessMessage"] = "Khóa tài khoản thành công!";
            return RedirectToAction(nameof(UserList));
        }

        [HttpPost]
        [Route("UnlockUser/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction(nameof(UserList));
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            TempData["SuccessMessage"] = "Mở khóa tài khoản thành công!";
            return RedirectToAction(nameof(UserList));
        }

        [HttpPost]
        [Route("DeleteUser/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || await _userManager.IsInRoleAsync(user, SD.Role_Admin))
            {
                TempData["ErrorMessage"] = "Không thể xóa tài khoản Admin hoặc người dùng không tồn tại.";
                return RedirectToAction(nameof(UserList));
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa người dùng.";
                return RedirectToAction(nameof(UserList));
            }

            TempData["SuccessMessage"] = "Xóa người dùng thành công!";
            return RedirectToAction(nameof(UserList));
        }

        [HttpGet]
        [Route("EditUser/{id}")]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || await _userManager.IsInRoleAsync(user, SD.Role_Admin))
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa tài khoản Admin hoặc người dùng không tồn tại.";
                return RedirectToAction(nameof(UserList));
            }

            var roles = await _userManager.GetRolesAsync(user);
            ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", roles.FirstOrDefault());
            return View("~/Views/Admin/EditUser.cshtml", user);
        }

        [HttpPost]
        [Route("EditUser/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, ApplicationUser user, string selectedRole)
        {
            if (id != user.Id) return NotFound();

            var existingUser = await _userManager.FindByIdAsync(id);
            if (existingUser == null || await _userManager.IsInRoleAsync(existingUser, SD.Role_Admin))
            {
                TempData["ErrorMessage"] = "Không thể chỉnh sửa tài khoản Admin hoặc người dùng không tồn tại.";
                return RedirectToAction(nameof(UserList));
            }

            if (!ModelState.IsValid)
            {
                ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                return View("~/Views/Admin/EditUser.cshtml", user);
            }

            try
            {
                existingUser.FullName = user.FullName;
                existingUser.Email = user.Email;
                existingUser.UserName = user.Email;
                existingUser.Address = user.Address;
                existingUser.Age = user.Age;

                var updateResult = await _userManager.UpdateAsync(existingUser);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                    return View("~/Views/Admin/EditUser.cshtml", user);
                }

                var currentRoles = await _userManager.GetRolesAsync(existingUser);
                await _userManager.RemoveFromRolesAsync(existingUser, currentRoles);
                await _userManager.AddToRoleAsync(existingUser, selectedRole);

                TempData["SuccessMessage"] = "Cập nhật người dùng thành công!";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật người dùng: {ex.Message}";
                ViewData["Roles"] = new SelectList(_roleManager.Roles.Where(r => r.Name != SD.Role_Admin), "Name", "Name", selectedRole);
                return View("~/Views/Admin/EditUser.cshtml", user);
            }
        }

        [HttpGet]
        [Route("ProductList")]
        public IActionResult ProductList()
        {
            var products = _context.Products
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Select(p => new
                {
                    p.ProductID,
                    p.ProductName,
                    p.Price,
                    Sizes = p.ProductSizes.Select(ps => new { ps.Size.SizeName, ps.Stock }).ToList()
                })
                .ToList();

            return View("~/Views/Admin/ProductList.cshtml", products);
        }

        [HttpGet]
        [Route("CreateProduct")]
        public IActionResult CreateProduct()
        {
            LoadProductViewData(null);
            return View("~/Views/Admin/CreateProduct.cshtml");
        }

        [HttpPost]
        [Route("CreateProduct")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles)
        {
            if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, imageFiles))
            {
                LoadProductViewData(product);
                return View("~/Views/Admin/CreateProduct.cshtml", product);
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
                LoadProductViewData(product);
                return View("~/Views/Admin/CreateProduct.cshtml", product);
            }
        }

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

            LoadProductViewData(product);
            ViewData["ExistingSizes"] = product.ProductSizes.Select(ps => ps.SizeID).ToList();
            ViewData["ExistingColors"] = product.ProductColors.Select(pc => pc.ColorID).ToList();
            ViewData["StockQuantities"] = product.ProductSizes.ToDictionary(ps => ps.SizeID, ps => ps.Stock);

            return View("~/Views/Admin/EditProduct.cshtml", product);
        }

        [HttpPost]
        [Route("EditProduct/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(int id, Product product, List<int> selectedSizes, List<int> selectedColors, List<int> stockQuantities, List<IFormFile> imageFiles, List<int> deletedImageIds, int? primaryImageId)
        {
            if (id != product.ProductID) return NotFound();

            if (!ModelState.IsValid || !ValidateProductInput(product, selectedSizes, selectedColors, null))
            {
                LoadProductViewData(product);
                await LoadExistingProductData(id);
                return View("~/Views/Admin/EditProduct.cshtml", product);
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
                LoadProductViewData(product);
                await LoadExistingProductData(id);
                return View("~/Views/Admin/EditProduct.cshtml", product);
            }
        }

        [HttpGet]
        [Route("DeleteProduct/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.ProductSizes).ThenInclude(ps => ps.Size)
                .Include(p => p.ProductColors).ThenInclude(pc => pc.Color)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(ProductList));
            }

            return View("~/Views/Admin/DeleteProduct.cshtml", product);
        }

        [HttpPost]
        [Route("DeleteProduct/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProductConfirmed(int id)
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

                if (await _context.OrderDetails.AnyAsync(od => od.ProductID == id))
                {
                    TempData["ErrorMessage"] = "Không thể xóa sản phẩm vì đã tồn tại trong đơn hàng.";
                    return RedirectToAction(nameof(ProductList));
                }

                _context.ProductImages.RemoveRange(product.ProductImages);
                _context.ProductSizes.RemoveRange(product.ProductSizes);
                _context.ProductColors.RemoveRange(product.ProductColors);
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

        [HttpGet]
        [Route("CategoryList")]
        public IActionResult CategoryList()
        {
            var categories = _context.Categories.ToList();
            return View("~/Views/Admin/CategoryList.cshtml", categories);
        }

        [HttpGet]
        [Route("CreateCategory")]
        public IActionResult CreateCategory()
        {
            return View("~/Views/Admin/CreateCategory.cshtml");
        }

        [HttpPost]
        [Route("CreateCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/CreateCategory.cshtml", category);
            }

            try
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Thêm danh mục thành công!";
                return RedirectToAction(nameof(CategoryList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm danh mục: {ex.Message}";
                return View("~/Views/Admin/CreateCategory.cshtml", category);
            }
        }

        [HttpGet]
        [Route("EditCategory/{id}")]
        public async Task<IActionResult> EditCategory(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryID == id);
            if (category == null) return NotFound();

            return View("~/Views/Admin/EditCategory.cshtml", category);
        }

        [HttpPost]
        [Route("EditCategory/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, Category category)
        {
            if (id != category.CategoryID) return NotFound();

            if (string.IsNullOrEmpty(category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Tên danh mục là bắt buộc.");
                return View("~/Views/Admin/EditCategory.cshtml", category);
            }

            try
            {
                var existingCategory = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryID == id);
                if (existingCategory == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy danh mục để cập nhật.";
                    return RedirectToAction(nameof(CategoryList));
                }

                existingCategory.CategoryName = category.CategoryName;
                existingCategory.Description = category.Description;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
                return RedirectToAction(nameof(CategoryList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật danh mục: {ex.Message}";
                return View("~/Views/Admin/EditCategory.cshtml", category);
            }
        }

        [HttpPost]
        [Route("DeleteCategory/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryID == id);
                if (category == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy danh mục để xóa.";
                    return RedirectToAction(nameof(CategoryList));
                }

                if (_context.Products.Any(p => p.CategoryID == id))
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục vì có sản phẩm liên quan.";
                    return RedirectToAction(nameof(CategoryList));
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa danh mục thành công!";
                return RedirectToAction(nameof(CategoryList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi xóa danh mục: {ex.Message}";
                return RedirectToAction(nameof(CategoryList));
            }
        }

        private void LoadProductViewData(Product product)
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product?.CategoryID);
            ViewData["Sizes"] = _context.Sizes.ToList();
            ViewData["Colors"] = _context.Colors.ToList();
        }

        private async Task LoadExistingProductData(int id)
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
        // Quản lý Kích thước (Size)
        [HttpGet]
        [Route("SizeList")]
        public IActionResult SizeList(string search, int page = 1, int pageSize = 10)
        {
            var query = _context.Sizes.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(s => s.SizeName.Contains(search));
            }

            var totalItems = query.Count();
            var sizes = query
                .OrderBy(s => s.SizeName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;

            return View("~/Views/Admin/SizeList.cshtml", sizes);
        }

        [HttpGet]
        [Route("CreateSize")]
        public IActionResult CreateSize()
        {
            return View("~/Views/Admin/CreateSize.cshtml");
        }

        [HttpPost]
        [Route("CreateSize")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSize(Size size)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/CreateSize.cshtml", size);
            }

            try
            {
                _context.Sizes.Add(size);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm kích thước thành công!";
                return RedirectToAction(nameof(SizeList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm kích thước: {ex.Message}";
                return View("~/Views/Admin/CreateSize.cshtml", size);
            }
        }

        [HttpGet]
        [Route("EditSize/{id}")]
        public async Task<IActionResult> EditSize(int? id)
        {
            if (id == null) return NotFound();

            var size = await _context.Sizes.FirstOrDefaultAsync(s => s.SizeID == id);
            if (size == null) return NotFound();

            return View("~/Views/Admin/EditSize.cshtml", size);
        }

        [HttpPost]
        [Route("EditSize/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSize(int id, Size size)
        {
            if (id != size.SizeID) return NotFound();

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/EditSize.cshtml", size);
            }

            try
            {
                var existingSize = await _context.Sizes.FirstOrDefaultAsync(s => s.SizeID == id);
                if (existingSize == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy kích thước để cập nhật.";
                    return RedirectToAction(nameof(SizeList));
                }

                existingSize.SizeName = size.SizeName;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật kích thước thành công!";
                return RedirectToAction(nameof(SizeList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật kích thước: {ex.Message}";
                return View("~/Views/Admin/EditSize.cshtml", size);
            }
        }

        [HttpPost]
        [Route("DeleteSize/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSize(int id)
        {
            try
            {
                var size = await _context.Sizes.FirstOrDefaultAsync(s => s.SizeID == id);
                if (size == null)
                {
                    return Json(new { success = false, error = "Không tìm thấy kích thước để xóa." });
                }

                if (await _context.ProductSizes.AnyAsync(ps => ps.SizeID == id))
                {
                    return Json(new { success = false, error = "Không thể xóa kích thước vì có sản phẩm liên quan." });
                }

                _context.Sizes.Remove(size);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa kích thước thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"Lỗi khi xóa kích thước: {ex.Message}" });
            }
        }

        [HttpGet]
        [Route("ColorList")]
        public IActionResult ColorList(string search, int page = 1, int pageSize = 10)
        {
            var query = _context.Colors.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.ColorName.Contains(search));
            }

            var totalItems = query.Count();
            var colors = query
                .OrderBy(c => c.ColorName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.Search = search;

            return View("~/Views/Admin/ColorList.cshtml", colors);
        }

        [HttpGet]
        [Route("CreateColor")]
        public IActionResult CreateColor()
        {
            return View("~/Views/Admin/CreateColor.cshtml");
        }

        [HttpPost]
        [Route("CreateColor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateColor(Color color)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/CreateColor.cshtml", color);
            }

            try
            {
                _context.Colors.Add(color);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm màu sắc thành công!";
                return RedirectToAction(nameof(ColorList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi thêm màu sắc: {ex.Message}";
                return View("~/Views/Admin/CreateColor.cshtml", color);
            }
        }

        [HttpGet]
        [Route("EditColor/{id}")]
        public async Task<IActionResult> EditColor(int? id)
        {
            if (id == null) return NotFound();

            var color = await _context.Colors.FirstOrDefaultAsync(c => c.ColorID == id);
            if (color == null) return NotFound();

            return View("~/Views/Admin/EditColor.cshtml", color);
        }

        [HttpPost]
        [Route("EditColor/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditColor(int id, Color color)
        {
            if (id != color.ColorID) return NotFound();

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/EditColor.cshtml", color);
            }

            try
            {
                var existingColor = await _context.Colors.FirstOrDefaultAsync(c => c.ColorID == id);
                if (existingColor == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy màu sắc để cập nhật.";
                    return RedirectToAction(nameof(ColorList));
                }

                existingColor.ColorName = color.ColorName;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật màu sắc thành công!";
                return RedirectToAction(nameof(ColorList));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật màu sắc: {ex.Message}";
                return View("~/Views/Admin/EditColor.cshtml", color);
            }
        }

        [HttpPost]
        [Route("DeleteColor/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteColor(int id)
        {
            try
            {
                var color = await _context.Colors.FirstOrDefaultAsync(c => c.ColorID == id);
                if (color == null)
                {
                    return Json(new { success = false, error = "Không tìm thấy màu sắc để xóa." });
                }

                if (await _context.ProductColors.AnyAsync(pc => pc.ColorID == id))
                {
                    return Json(new { success = false, error = "Không thể xóa màu sắc vì có sản phẩm liên quan." });
                }

                _context.Colors.Remove(color);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Xóa màu sắc thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = $"Lỗi khi xóa màu sắc: {ex.Message}" });
            }
        }
    }
}