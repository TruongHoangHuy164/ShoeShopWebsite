using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Areas.Admin.Controllers
{
    [Authorize(Roles = SD.Role_Admin)]
    [Route("Admin")]
    public class DiscountController : Controller
    {
        private readonly NikeShopDbContext _context;
        private readonly ILogger<DiscountController> _logger;

        public DiscountController(NikeShopDbContext context, ILogger<DiscountController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Route("ListDiscountCodes")]
        public async Task<IActionResult> ListDiscountCodes(
            int page = 1,
            int pageSize = 10,
            string search = "",
            string status = "",
            bool? isExpired = null)
        {
            try
            {
                // Thêm header chống cache
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Add("Pragma", "no-cache");
                Response.Headers.Add("Expires", "0");

                if (_context.DiscountCodes == null)
                {
                    _logger.LogWarning("DiscountCodes DbSet is null.");
                    TempData["ErrorMessage"] = "Không thể truy cập danh sách mã giảm giá.";
                    return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", new List<DiscountCode>());
                }

                var query = _context.DiscountCodes.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.Trim();
                    query = query.Where(dc => EF.Functions.Collate(dc.Code, "SQL_Latin1_General_CP1_CI_AS").Contains(search) ||
                                             EF.Functions.Collate(dc.DiscountType.ToString(), "SQL_Latin1_General_CP1_CI_AS").Contains(search) ||
                                             dc.DiscountValue.ToString().Contains(search));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    if (bool.TryParse(status, out bool isActive))
                    {
                        query = query.Where(dc => dc.IsActive == isActive);
                    }
                }

                if (isExpired.HasValue)
                {
                    var today = DateTime.Today;
                    query = query.Where(dc => isExpired.Value ? dc.ExpiryDate < today : dc.ExpiryDate >= today);
                }

                var totalItems = await query.CountAsync();

                var discountCodes = await query
                    .OrderBy(dc => dc.Code)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!string.IsNullOrEmpty(search))
                {
                    discountCodes = discountCodes
                        .Where(dc => dc.ExpiryDate.ToString("dd/MM/yyyy").Contains(search))
                        .ToList();
                    totalItems = discountCodes.Count;
                }

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = totalItems;
                ViewBag.Search = search;
                ViewBag.Status = status;
                ViewBag.IsExpired = isExpired;

                _logger.LogInformation("Successfully loaded {Count} discount codes for page {Page}.", discountCodes.Count, page);
                return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", discountCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading discount codes for page {Page}.", page);
                TempData["ErrorMessage"] = "Lỗi khi tải danh sách mã giảm giá. Vui lòng thử lại.";
                return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", new List<DiscountCode>());
            }
        }
        [HttpGet]
        [Route("CreateDiscountCode")]
        public IActionResult CreateDiscountCode()
        {
            _logger.LogInformation("CreateDiscountCode GET action called at {Time}.", DateTime.Now);
            return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", new DiscountCode());
        }

        [HttpPost]
        [Route("CreateDiscountCode")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDiscountCode(DiscountCode discountCode)
        {
            _logger.LogInformation("CreateDiscountCode POST called at {Time} with Code: {Code}", DateTime.Now, discountCode?.Code);
            _logger.LogInformation("Received raw form data: {RawData}", Request.Form.ToString());
            _logger.LogInformation("Bound model: Code={Code}, DiscountType={DiscountType}, DiscountValue={DiscountValue}, StartDate={StartDate}, ExpiryDate={ExpiryDate}, MaxUsage={MaxUsage}, MinOrderValue={MinOrderValue}, IsActive={IsActive}, UsageCount={UsageCount}",
                discountCode?.Code, discountCode?.DiscountType, discountCode?.DiscountValue, discountCode?.StartDate, discountCode?.ExpiryDate, discountCode?.MaxUsage, discountCode?.MinOrderValue, discountCode?.IsActive, discountCode?.UsageCount);

            if (discountCode == null)
            {
                _logger.LogWarning("DiscountCode object is null at {Time}", DateTime.Now);
                TempData["ErrorMessage"] = "Dữ liệu mã giảm giá không hợp lệ.";
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", new DiscountCode());
            }

            discountCode.Code = discountCode.Code?.Trim().ToLowerInvariant() ?? string.Empty;
            discountCode.UsageCount = 0;

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid for Code: {Code} at {Time}. Errors: {Errors}", discountCode.Code, DateTime.Now, string.Join(", ", errors));
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ: " + string.Join(", ", errors);
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }

            var currentDate = DateTime.Today;
            if (discountCode.StartDate < currentDate)
            {
                ModelState.AddModelError("StartDate", "Ngày bắt đầu không được nhỏ hơn ngày hiện tại.");
                _logger.LogWarning("StartDate validation failed for Code: {Code}. StartDate: {StartDate}, CurrentDate: {CurrentDate}", discountCode.Code, discountCode.StartDate, currentDate);
            }

            if (discountCode.ExpiryDate < discountCode.StartDate)
            {
                ModelState.AddModelError("ExpiryDate", "Ngày hết hạn phải lớn hơn hoặc bằng ngày bắt đầu.");
                _logger.LogWarning("ExpiryDate validation failed for Code: {Code}. ExpiryDate: {ExpiryDate}, StartDate: {StartDate}", discountCode.Code, discountCode.ExpiryDate, discountCode.StartDate);
            }

            if (discountCode.DiscountType == DiscountTypeEnum.Percentage && (discountCode.DiscountValue <= 0 || discountCode.DiscountValue > 100))
            {
                ModelState.AddModelError("DiscountValue", "Giá trị giảm giá phải từ 0 đến 100% khi chọn loại Phần trăm.");
                _logger.LogWarning("DiscountValue validation failed for Code: {Code}. DiscountType: {Type}, Value: {Value}", discountCode.Code, discountCode.DiscountType, discountCode.DiscountValue);
            }
            else if (discountCode.DiscountType == DiscountTypeEnum.Fixed && discountCode.DiscountValue <= 0)
            {
                ModelState.AddModelError("DiscountValue", "Giá trị giảm giá phải lớn hơn 0 khi chọn loại Số tiền cố định.");
                _logger.LogWarning("DiscountValue validation failed for Code: {Code}. DiscountType: {Type}, Value: {Value}", discountCode.Code, discountCode.DiscountType, discountCode.DiscountValue);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid after validations for Code: {Code} at {Time}. Errors: {Errors}", discountCode.Code, DateTime.Now, string.Join(", ", errors));
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ: " + string.Join(", ", errors);
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }

            _logger.LogInformation("Checking for duplicate code: {Code} at {Time}", discountCode.Code, DateTime.Now);
            var existingCode = await _context.DiscountCodes
                .Where(dc => dc.Code == discountCode.Code)
                .FirstOrDefaultAsync();

            if (existingCode != null)
            {
                _logger.LogWarning("Duplicate discount code found: {Code} at {Time}", discountCode.Code, DateTime.Now);
                ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại.");
                TempData["ErrorMessage"] = "Mã giảm giá đã tồn tại.";
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }

            try
            {
                _logger.LogInformation("Saving discount code: {Code} at {Time}", discountCode.Code, DateTime.Now);
                _context.DiscountCodes.Add(discountCode);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved discount code: {Code} at {Time}", discountCode.Code, DateTime.Now);

                TempData["SuccessMessage"] = "Thêm mã giảm giá thành công!";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating discount code: {Code} at {Time}", discountCode.Code, DateTime.Now);
                TempData["ErrorMessage"] = "Lỗi cơ sở dữ liệu khi thêm mã giảm giá: " + (ex.InnerException?.Message ?? ex.Message);
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating discount code: {Code} at {Time}", discountCode.Code, DateTime.Now);
                TempData["ErrorMessage"] = "Lỗi không xác định: " + ex.Message;
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }
        }


        [HttpGet]
        [Route("UpdateDiscountCode/{id}")]
        public async Task<IActionResult> UpdateDiscountCode(int id)
        {
            try
            {
                if (id <= 0)
                {
                    _logger.LogWarning("Invalid discount code ID: {Id}", id);
                    TempData["ErrorMessage"] = "ID mã giảm giá không hợp lệ.";
                    return NotFound(new { error = $"ID mã giảm giá {id} không hợp lệ." });
                }

                _logger.LogInformation("Attempting to fetch discount code with ID: {Id}", id);
                var discountCode = await _context.DiscountCodes.FindAsync(id);
                if (discountCode == null)
                {
                    _logger.LogWarning("Discount code with ID {Id} not found.", id);
                    TempData["ErrorMessage"] = "Không tìm thấy mã giảm giá.";
                    return NotFound(new { error = $"Mã giảm giá với ID {id} không tồn tại." });
                }

                return View("~/Areas/Admin/Views/Discount/UpdateDiscountCode.cshtml", discountCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching discount code with ID: {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi tải mã giảm giá: {ex.Message}";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
        }


        [HttpPost]
        [Route("UpdateDiscountCode/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDiscountCode(int id, [FromForm] DiscountCode discountCode)
        {
            _logger.LogInformation("UpdateDiscountCode POST called for ID: {Id} at {Time}", id, DateTime.Now);
            _logger.LogInformation("Received form data: {Data}", Request.Form.ToString());

            if (id != discountCode?.DiscountCodeID)
            {
                _logger.LogWarning("ID mismatch: Route ID {Id} vs Form ID {FormId}", id, discountCode?.DiscountCodeID);
                return Json(new { success = false, error = "ID không khớp." });
            }

            if (discountCode == null)
            {
                _logger.LogWarning("DiscountCode object is null at {Time}", DateTime.Now);
                return Json(new { success = false, error = "Dữ liệu không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState invalid for ID: {Id}. Errors: {Errors}", id, string.Join(", ", errors));
                return Json(new { success = false, errors = errors });
            }

            try
            {
                _logger.LogInformation("Attempting to update discount code with ID: {Id}", id);
                var existingDiscountCode = await _context.DiscountCodes.FindAsync(id);
                if (existingDiscountCode == null)
                {
                    _logger.LogWarning("Discount code with ID {Id} not found for update.", id);
                    Response.StatusCode = 404;
                    return Json(new { success = false, error = "Không tìm thấy mã giảm giá để cập nhật." });
                }

                if (await _context.DiscountCodes.AnyAsync(dc => dc.Code == discountCode.Code && dc.DiscountCodeID != id))
                {
                    return Json(new { success = false, error = "Mã giảm giá đã tồn tại." });
                }

                var currentDate = DateTime.Today;
                if (discountCode.StartDate < currentDate)
                {
                    return Json(new { success = false, error = "Ngày bắt đầu không được nhỏ hơn ngày hiện tại." });
                }

                if (discountCode.ExpiryDate < discountCode.StartDate)
                {
                    return Json(new { success = false, error = "Ngày hết hạn phải lớn hơn hoặc bằng ngày bắt đầu." });
                }

                if (discountCode.DiscountType == DiscountTypeEnum.Percentage && (discountCode.DiscountValue <= 0 || discountCode.DiscountValue > 100))
                {
                    return Json(new { success = false, error = "Giá trị giảm giá phải từ 0 đến 100% khi chọn loại Phần trăm." });
                }
                else if (discountCode.DiscountType == DiscountTypeEnum.Fixed && discountCode.DiscountValue <= 0)
                {
                    return Json(new { success = false, error = "Giá trị giảm giá phải lớn hơn 0 khi chọn loại Số tiền cố định." });
                }

                existingDiscountCode.Code = discountCode.Code;
                existingDiscountCode.DiscountValue = discountCode.DiscountValue;
                existingDiscountCode.DiscountType = discountCode.DiscountType;
                existingDiscountCode.StartDate = discountCode.StartDate;
                existingDiscountCode.ExpiryDate = discountCode.ExpiryDate;
                existingDiscountCode.MaxUsage = discountCode.MaxUsage;
                existingDiscountCode.MinOrderValue = discountCode.MinOrderValue;
                existingDiscountCode.IsActive = discountCode.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated discount code with ID: {Id}", id);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discount code with ID: {Id}", id);
                return Json(new { success = false, error = $"Lỗi khi cập nhật mã giảm giá: {ex.Message}" });
            }
        }

        [HttpPost]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDiscountCode(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to delete discount code with ID: {Id}", id);
                var discountCode = await _context.DiscountCodes.FindAsync(id);
                if (discountCode == null)
                {
                    _logger.LogWarning("Discount code with ID {Id} not found for deletion.", id);
                    TempData["ErrorMessage"] = "Không tìm thấy mã giảm giá để xóa.";
                    return Json(new { success = false, error = "Không tìm thấy mã giảm giá." });
                }

                _context.DiscountCodes.Remove(discountCode);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted discount code with ID: {Id}", id);
                TempData["SuccessMessage"] = "Xóa mã giảm giá thành công!";
                var redirectUrl = Url.Action(nameof(ListDiscountCodes), "Discount", new { area = "Admin" });
                return Json(new { success = true, message = "Xóa mã giảm giá thành công!", redirectUrl = redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting discount code with ID: {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi xóa mã giảm giá: {ex.Message}";
                return Json(new { success = false, error = $"Lỗi khi xóa mã giảm giá: {ex.Message}" });
            }
        }

        
    }
}