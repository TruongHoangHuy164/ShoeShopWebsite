using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Services
{
    public interface IMomoService
    {
        Task<MomoResponse> CreatePaymentAsync(Order order);
        MomoCallback PaymentExecuteAsync(IQueryCollection query);
    }
}