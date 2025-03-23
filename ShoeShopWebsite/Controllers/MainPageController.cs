using Microsoft.AspNetCore.Mvc;

namespace ShoeShopWebsite.Controllers
{
    public class MainPageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
