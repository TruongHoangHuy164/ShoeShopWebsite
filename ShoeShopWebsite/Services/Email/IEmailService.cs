using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendOrderConfirmationEmailAsync(OrderEmailModel order);
    }
}