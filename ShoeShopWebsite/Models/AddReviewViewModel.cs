using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class AddReviewViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn số sao đánh giá")]
        [Range(1, 5, ErrorMessage = "Đánh giá phải từ 1 đến 5 sao")]
        public int? Rating { get; set; }

        [StringLength(500, ErrorMessage = "Bình luận không được vượt quá 500 ký tự")]
        public string? Comment { get; set; }

        // Thuộc tính hiển thị
        public string? ProductName { get; set; }
        public string? SizeName { get; set; }
        public string? ColorName { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Price { get; set; }
    }
}
