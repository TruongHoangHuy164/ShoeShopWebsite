namespace ShoeShopWebsite.Models
{
    public class BestSellingProduct
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
    }
}
