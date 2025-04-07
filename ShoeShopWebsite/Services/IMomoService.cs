using ShoeShopWebsite.Models;
using ShoeShopWebsite.Models.Momo;

namespace ShoeShopWebsite.Services
{
    public interface IMomoService
    {
        Task<MomoCreatePaymentResponseModel> CreatePaymentAsync(OrderInfoModel model); // Sửa tên thành CreatePaymentAsync
        MomoExecuteResponseModel PaymentExecuteAsync(IQueryCollection collection);
    }
}