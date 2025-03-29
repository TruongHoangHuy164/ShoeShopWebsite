using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Order
    {
        public int OrderID { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [StringLength(50)]
        public string PaymentMethod { get; set; } // Thêm trường này để lưu phương thức thanh toán

        public Color Color { get; set; } // Quan hệ với Color

        public ICollection<OrderDetail> OrderDetails { get; set; }
    }
}