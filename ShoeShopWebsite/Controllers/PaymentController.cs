using Microsoft.AspNetCore.Mvc;
using ShoeShopWebsite.Models.Vnpay;
using ShoeShopWebsite.Services.NewFolder;
using ShoeShopWebsite.Services.VnPay;

namespace ShoeShopWebsite.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IVNPayService _vnPayService;
        public PaymentController( IVNPayService vnPayService)
        {
           
            _vnPayService = vnPayService;


        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
        {
            var url = _vnPayService.CreatePaymentUrl(model, HttpContext);


            return Redirect(url);
        }
    }
}
