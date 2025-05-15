using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ShoeShopWebsite.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ShoeShopWebsite.Hubs;

namespace ShoeShopWebsite.Controllers
{
    public class ChatController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableEmployee()
        {
            try
            {
                // Lấy danh sách nhân viên và admin
                var employees = await _userManager.GetUsersInRoleAsync(SD.Role_Employee);
                var admins = await _userManager.GetUsersInRoleAsync(SD.Role_Admin);
                var activeUsers = employees.Concat(admins).Distinct().ToList();

                // Lấy danh sách ID nhân viên đang trực tuyến từ ChatHub
                var onlineStaffIds = ChatHub.GetActiveStaffIds();

                // Lọc danh sách nhân viên trực tuyến
                var onlineEmployees = activeUsers
                    .Where(u => onlineStaffIds.Contains(u.Id))
                    .Select(u => new { id = u.Id, userName = u.UserName })
                    .ToList();

                if (onlineEmployees.Any())
                {
                    return Json(new { success = true, employees = onlineEmployees });
                }

                return Json(new { success = false, message = "Hiện không có nhân viên hỗ trợ nào trực tuyến." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi server: {ex.Message}" });
            }
        }
    }
}