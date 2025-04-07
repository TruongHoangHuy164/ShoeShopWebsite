using Microsoft.AspNetCore.Mvc;

namespace ShoeShopWebsite.Controllers
{
    public class AboutUsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
