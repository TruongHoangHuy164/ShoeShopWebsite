using ShoeShopWebsite.Models;
using ShoeShopWebsite.Models.Vnpay;

namespace ShoeShopWebsite.Services.VnPay
{
    public interface IVNPayService
    {
        string CreatePaymentUrl(PaymentInformationModel paymentInfo, HttpContext context);
        PaymentResponseModel PaymentExecute(IQueryCollection collections);


    }
}