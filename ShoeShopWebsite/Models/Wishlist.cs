namespace ShoeShopWebsite.Models
{
    public class Wishlist
    {
        public int WishlistID { get; set; }
        public string UserID { get; set; }
        public int ProductID { get; set; }
        public Product Product { get; set; }
    }
}
