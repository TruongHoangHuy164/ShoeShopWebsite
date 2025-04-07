using Microsoft.AspNetCore.Mvc;

namespace ShoeShopWebsite.Controllers
{
    public class ContactController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Submit(string name, string email, string phone, string message)
        {
            // Xử lý dữ liệu gửi lên (ví dụ: lưu vào database, gửi email, v.v.)
            try
            {
                // Giả lập xử lý thành công
                // Thực tế: Thêm logic gửi email hoặc lưu vào DB ở đây
                return Json(new { success = true, message = "Tin nhắn đã được gửi!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}