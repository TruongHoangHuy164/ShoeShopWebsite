using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Services
{
    public interface IVNPayService
    {
        string CreatePaymentUrl(Order order, string ipAddress);
        VNPayResponse PaymentExecute(IQueryCollection query);
        string ComputeHmacSha512(string message, string key);
    }
}