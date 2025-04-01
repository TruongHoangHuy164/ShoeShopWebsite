using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Order
    {
        public int OrderID { get; set; }
        public string SessionId { get; set; }
        public string FullName { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        public string Note { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public List<OrderDetail> OrderDetails { get; set; }
    }
}