using System.Net.Mail;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
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
                await client.ConnectAsync(emailSettings["SmtpServer"], int.Parse(emailSettings["SmtpPort"]), MailKit.Security.SecureSocketOptions.StartTls);

                // Xác thực
                await client.AuthenticateAsync(emailSettings["Username"], emailSettings["Password"]);

                // Gửi email
                await client.SendAsync(email);

                // Ngắt kết nối
                await client.DisconnectAsync(true);
            }
        }
    }
}