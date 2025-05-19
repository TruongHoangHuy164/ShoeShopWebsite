using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Order
    {
        public int OrderID { get; set; }

        [Required]
        public string SessionId { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        public string? Note { get; set; } // Nullable để phù hợp với trường hợp không nhập ghi chú

        [Required]
        public decimal TotalPrice { get; set; }

        [Required]
        public DateTime OrderDate { get; set; }

        [Required]
        public string Status { get; set; }

        [Required]
        public string PaymentMethod { get; set; }
        public int? DiscountCodeID { get; set; } // Thêm trường này (nullable)
        public decimal? DiscountAmount { get; set; } // Thêm trường này (nullable)
        public DiscountCode? DiscountCode { get; set; } // Navigation property
        public List<OrderDetail> OrderDetails { get; set; }
    }
}