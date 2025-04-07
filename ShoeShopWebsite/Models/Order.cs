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

        public List<OrderDetail> OrderDetails { get; set; }
    }
}