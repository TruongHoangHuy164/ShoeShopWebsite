using System.ComponentModel.DataAnnotations;
using ShoeShopWebsite.Models;

namespace ShoeShopWebsite.Models
{
    public class CheckoutViewModel
    {
        public List<Cart>? CartItems { get; set; }

        [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        public string? FullName { get; set; }

        public int ProvinceId { get; set; }
        public int DistrictId { get; set; }
        public int WardId { get; set; }

        [Required(ErrorMessage = "Địa chỉ chi tiết là bắt buộc.")]
        public string? AddressDetail { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và có đúng 10 chữ số.")]
        public string? PhoneNumber { get; set; }

        public string? Note { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
        public string? PaymentMethod { get; set; }
    }
}