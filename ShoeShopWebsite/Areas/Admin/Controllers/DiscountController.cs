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
        private readonly ILogger _logger;
        public DiscountController(NikeShopDbContext context, ILogger<DiscountController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Route("ListDiscountCodes")]
        public async Task<IActionResult> ListDiscountCodes(int page = 1, int pageSize = 10)
        {
            try
            {
                if (_context.DiscountCodes == null)
                {
                    TempData["ErrorMessage"] = "Không thể truy cập danh sách mã giảm giá.";
                    return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", new List<DiscountCode>());
                }

                var discountCodes = await _context.DiscountCodes
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.Page = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = await _context.DiscountCodes.CountAsync();

                return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", discountCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading discount codes.");
                TempData["ErrorMessage"] = $"Lỗi khi tải danh sách mã giảm giá: {ex.Message}";
                return View("~/Areas/Admin/Views/Discount/ListDiscountCodes.cshtml", new List<DiscountCode>());
            }
        }

        [HttpGet]
        [Route("CreateDiscountCode")]
        public IActionResult CreateDiscountCode()
        {
            _logger.LogInformation("CreateDiscountCode GET action called.");
            return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDiscountCode(DiscountCode discountCode)
        {
            _logger.LogInformation("CreateDiscountCode POST called with Code: {Code}, Type: {Type}, Value: {Value}",
                discountCode?.Code, discountCode?.DiscountType, discountCode?.DiscountValue);

            // Xóa lỗi Range cho DiscountValue nếu là Fixed
            if (discountCode.DiscountType == "Fixed" && discountCode.DiscountValue > 100)
            {
                ModelState.Remove("DiscountValue");
            }

            // Kiểm tra ExpiryDate >= StartDate
            if (discountCode.ExpiryDate < discountCode.StartDate)
            {
                ModelState.AddModelError("ExpiryDate", "Ngày hết hạn phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("ModelState invalid. Errors: {Errors}", string.Join(", ", errors));
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }

            try
            {
                if (await _context.DiscountCodes.AnyAsync(dc => dc.Code == discountCode.Code))
                {
                    ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại.");
                    return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
                }

                _context.DiscountCodes.Add(discountCode);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created discount code: {Code}", discountCode.Code);
                TempData["SuccessMessage"] = "Thêm mã giảm giá thành công!";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating discount code: {Code}", discountCode?.Code);
                TempData["ErrorMessage"] = "Lỗi cơ sở dữ liệu khi thêm mã giảm giá. Vui lòng thử lại.";
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating discount code: {Code}", discountCode?.Code);
                TempData["ErrorMessage"] = $"Lỗi khi thêm mã giảm giá: {ex.Message}";
                return View("~/Areas/Admin/Views/Discount/CreateDiscountCode.cshtml", discountCode);
            }
        }
        // GET: /Admin/Discount/Edit/{id}
        [HttpGet]
        [Route("Edit/{id}")]
        public async Task<IActionResult> UpdateDiscountCode(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to fetch discount code with ID: {Id}", id);
                var discountCode = await _context.DiscountCodes.FindAsync(id);
                if (discountCode == null)
                {
                    _logger.LogWarning("Discount code with ID {Id} not found.", id);
                    TempData["ErrorMessage"] = "Không tìm thấy mã giảm giá.";
                    return RedirectToAction(nameof(ListDiscountCodes));
                }

                return View("~/Areas/Admin/Views/Discount/Edit.cshtml", discountCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching discount code with ID: {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi tải mã giảm giá: {ex.Message}";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
        }

        // POST: /Admin/Discount/Edit/{id}
        [HttpPost]
        [Route("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDiscountCode(int id, DiscountCode discountCode)
        {
            if (id != discountCode.DiscountCodeID)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/Discount/Edit.cshtml", discountCode);
            }

            try
            {
                _logger.LogInformation("Attempting to update discount code with ID: {Id}", id);
                var existingDiscountCode = await _context.DiscountCodes.FindAsync(id);
                if (existingDiscountCode == null)
                {
                    _logger.LogWarning("Discount code with ID {Id} not found for update.", id);
                    TempData["ErrorMessage"] = "Không tìm thấy mã giảm giá để cập nhật.";
                    return RedirectToAction(nameof(ListDiscountCodes));
                }

                if (await _context.DiscountCodes.AnyAsync(dc => dc.Code == discountCode.Code && dc.DiscountCodeID != id))
                {
                    ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại.");
                    return View("~/Areas/Admin/Views/Discount/Edit.cshtml", discountCode);
                }

                existingDiscountCode.Code = discountCode.Code;
                existingDiscountCode.DiscountValue = discountCode.DiscountValue;
                existingDiscountCode.DiscountType = discountCode.DiscountType;
                existingDiscountCode.StartDate = discountCode.StartDate;
                existingDiscountCode.ExpiryDate = discountCode.ExpiryDate;
                existingDiscountCode.MaxUsage = discountCode.MaxUsage;
                existingDiscountCode.UsageCount = discountCode.UsageCount;
                existingDiscountCode.MinOrderValue = discountCode.MinOrderValue;
                existingDiscountCode.IsActive = discountCode.IsActive;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated discount code with ID: {Id}", id);
                TempData["SuccessMessage"] = "Cập nhật mã giảm giá thành công!";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discount code with ID: {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi cập nhật mã giảm giá: {ex.Message}";
                return View("~/Areas/Admin/Views/Discount/Edit.cshtml", discountCode);
            }
        }

        // POST: /Admin/Discount/Delete/{id}
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
                    return RedirectToAction(nameof(ListDiscountCodes));
                }

                _context.DiscountCodes.Remove(discountCode);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted discount code with ID: {Id}", id);
                TempData["SuccessMessage"] = "Xóa mã giảm giá thành công!";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting discount code with ID: {Id}", id);
                TempData["ErrorMessage"] = $"Lỗi khi xóa mã giảm giá: {ex.Message}";
                return RedirectToAction(nameof(ListDiscountCodes));
            }
        }
    }
}


