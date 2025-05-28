namespace ShoeShopWebsite.Models
{
    public class ReviewViewModel
    {
        public int ReviewID { get; set; }
        public string ProductName { get; set; }
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public DateTime ReviewDate { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
    }
}
