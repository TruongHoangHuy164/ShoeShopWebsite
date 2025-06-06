﻿namespace ShoeShopWebsite.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<object> MonthlyRevenue { get; set; }
        public List<object> QuarterlyRevenue { get; set; }
        public (List<string> CategoryNames, List<object> Revenues) CategoryRevenueData { get; set; }
        public List<BestSellingProduct> BestSellingProducts { get; set; }
        public int DiscountCodeCount { get; set; }
        public int TotalReviews { get; set; } // Thêm mới
    }
}
