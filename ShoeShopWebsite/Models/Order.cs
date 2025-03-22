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

        public ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
