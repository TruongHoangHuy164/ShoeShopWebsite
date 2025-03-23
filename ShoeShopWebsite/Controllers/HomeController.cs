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

        public HomeController(ILogger<HomeController> logger, NikeShopDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        // GET: Home
        public async Task<IActionResult> Index()
        {
            // L?y danh sách s?n ph?m
            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.ProductColors)
                .ThenInclude(pc => pc.Color)
                .ToListAsync();

            // L?y danh sách kích th??c
            var sizes = await _context.Sizes.ToListAsync();

            // Gán danh sách kích th??c vào ViewBag
            ViewBag.Sizes = sizes;

            return View(products);
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