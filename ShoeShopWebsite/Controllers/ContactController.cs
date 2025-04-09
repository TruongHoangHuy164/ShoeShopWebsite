using Microsoft.AspNetCore.Mvc;
using ShoeShopWebsite.Services;

namespace ShoeShopWebsite.Controllers
{
    public class ContactController : Controller
    {
        private readonly IEmailService _emailService;

        public ContactController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Submit(string name, string email, string phone, string message)
        {
            try
            {
                // Tạo nội dung email
                var subject = "Tin nhắn mới từ biểu mẫu liên hệ";
                var body = $"<h2>Tin nhắn từ khách hàng</h2>" +
                           $"<p><strong>Họ và tên:</strong> {name}</p>" +
                           $"<p><strong>Email:</strong> {email}</p>" +
                           $"<p><strong>Số điện thoại:</strong> {phone ?? "Không cung cấp"}</p>" +
                           $"<p><strong>Tin nhắn:</strong> {message}</p>" +
                           $"<p>---</p>" +
                           $"<p>Được gửi từ ShoeShopWebsite vào {DateTime.Now:dd/MM/yyyy HH:mm}</p>";

                // Gửi email đến địa chỉ hỗ trợ
                await _emailService.SendEmailAsync("support@shoeshop.vn", subject, body);

                return Json(new { success = true, message = "Tin nhắn đã được gửi! Chúng tôi sẽ liên hệ với bạn sớm." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}