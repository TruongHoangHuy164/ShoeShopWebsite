using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class DiscountCode
    {
        [Key]
        public int DiscountCodeID { get; set; }

        [Required(ErrorMessage = "Mã giảm giá là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Mã giảm giá không được vượt quá 50 ký tự.")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Loại giảm giá là bắt buộc.")]
        public DiscountTypeEnum DiscountType { get; set; }

        [Required(ErrorMessage = "Giá trị giảm giá là bắt buộc.")]
        public decimal DiscountValue { get; set; }

        [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Ngày hết hạn là bắt buộc.")]
        public DateTime ExpiryDate { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Số lần sử dụng tối đa phải là số không âm.")]
        public int MaxUsage { get; set; }

        public int UsageCount { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn hàng tối thiểu phải là số không âm.")]
        public decimal MinOrderValue { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public enum DiscountTypeEnum
    {
        Percentage, 
        Fixed
    }
}