using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeShopWebsite.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ShoeShopWebsite.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly NikeShopDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ILogger<HomeController> logger, NikeShopDbContext context, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors)
                .ThenInclude(pc => pc.Color)
                .ToListAsync();
            var sizes = await _context.Sizes.ToListAsync();

            ViewBag.Sizes = sizes;
            return View(products);
        }
        [Authorize]
        public async Task<IActionResult> Chat()
        {
            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User is not authenticated. Redirecting to login.");
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogError("User not found in database despite being authenticated.");
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == SD.Role_Customer)
            {
                var employees = await _userManager.GetUsersInRoleAsync(SD.Role_Employee);
                if (!employees.Any())
                {
                    _logger.LogWarning("No employees found with Role_Employee.");
                    TempData["ErrorMessage"] = "Hiện không có nhân viên hỗ trợ online.";
                    return RedirectToAction("Index");
                }

                var defaultEmployee = employees.First();
                _logger.LogInformation($"Assigning employee ID: {defaultEmployee.Id}, Username: {defaultEmployee.UserName} for chat.");
                ViewData["EmployeeId"] = defaultEmployee.Id;
                return View("~/Views/Home/Chat.cshtml");
            }

            _logger.LogInformation($"User with role {role} attempted to access customer chat. Redirecting to Index.");
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}