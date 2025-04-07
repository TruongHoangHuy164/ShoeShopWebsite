using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class CheckoutViewModel
    {
        public List<Cart>? CartItems { get; set; }

        [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Tỉnh/Thành phố là bắt buộc.")]
        public string? Province { get; set; }

        [Required(ErrorMessage = "Quận/Huyện là bắt buộc.")]
        public string? District { get; set; }

        [Required(ErrorMessage = "Phường/Xã là bắt buộc.")]
        public string? Ward { get; set; }

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