namespace ShoeShopWebsite.Models
{
    public class OrderItemEmailModel
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
    }
}
