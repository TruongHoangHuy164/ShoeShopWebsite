using Microsoft.Extensions.Configuration;
using MimeKit;
using ShoeShopWebsite.Models;
using System.Text;
using System.Threading.Tasks;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ShoeShopWebsite.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            // Tạo email message
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(emailSettings["SenderName"], emailSettings["SenderEmail"]));
            email.To.Add(new MailboxAddress("", toEmail));
            email.Subject = subject;

            // Tạo nội dung email
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            email.Body = bodyBuilder.ToMessageBody();

            // Gửi email qua SMTP
            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(emailSettings["SmtpServer"], int.Parse(emailSettings["Port"]), MailKit.Security.SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(emailSettings["Username"], emailSettings["Password"]);
                await client.SendAsync(email);
                await client.DisconnectAsync(true);
            }
        }

        public async Task SendOrderConfirmationEmailAsync(OrderEmailModel order)
        {
            var subject = $"Xác nhận đơn hàng #{order.OrderId}";
            var body = BuildOrderEmailBody(order);
            await SendEmailAsync(order.CustomerEmail, subject, body);
        }

        private string BuildOrderEmailBody(OrderEmailModel order)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='utf-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Helvetica Neue', Arial, sans-serif; color: #333; }");
            sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine(".header { background-color: #f5f5f5; padding: 10px; text-align: center; }");
            sb.AppendLine(".table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            sb.AppendLine(".table th, .table td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine(".table th { background-color: #f5f5f5; }");
            sb.AppendLine(".footer { text-align: center; font-size: 12px; color: #777; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h2>Cảm ơn bạn đã mua sắm tại ShoeShop!</h2>");
            sb.AppendLine("</div>");
            sb.AppendLine($"<p>Xin chào {order.CustomerName},</p>");
            sb.AppendLine($"<p>Đơn hàng #{order.OrderId} của bạn đã được xác nhận thanh toán thành công vào ngày {order.OrderDate:dd/MM/yyyy HH:mm}.</p>");
            sb.AppendLine("<h3>Chi tiết đơn hàng</h3>");
            sb.AppendLine("<table class='table'>");
            sb.AppendLine("<tr><th>Sản phẩm</th><th>Số lượng</th><th>Giá</th><th>Kích cỡ</th><th>Màu sắc</th></tr>");
            foreach (var item in order.Items)
            {
                sb.AppendLine($"<tr><td>{item.ProductName}</td><td>{item.Quantity}</td><td>{item.Price:N0} VND</td><td>{item.Size}</td><td>{item.Color}</td></tr>");
            }
            sb.AppendLine("</table>");
            sb.AppendLine($"<p><strong>Tổng tiền:</strong> {order.TotalAmount:N0} VND</p>");
            sb.AppendLine($"<p><strong>Địa chỉ giao hàng:</strong> {order.ShippingAddress}</p>");
            sb.AppendLine($"<p><strong>Phương thức thanh toán:</strong> {order.PaymentMethod}</p>");
            sb.AppendLine("<h3>Thông tin liên hệ</h3>");
            sb.AppendLine("<p>Nếu có thắc mắc, vui lòng liên hệ qua email: support@shoeshop.com hoặc số điện thoại: (123) 456-7890.</p>");
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>© 2025 ShoeShop. All rights reserved.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }
    }
}