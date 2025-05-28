namespace ShoeShopWebsite.Models
{
    public class OrderEmailModel
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string ShippingAddress { get; set; }
        public string PaymentMethod { get; set; }
        public List<OrderItemEmailModel> Items { get; set; } = new List<OrderItemEmailModel>();
    }
}
