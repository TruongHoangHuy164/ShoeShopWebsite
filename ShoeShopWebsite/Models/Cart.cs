using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Cart
    {
        public int CartID { get; set; }

        [Required]
        public int ProductID { get; set; }
        public Product Product { get; set; }

        [Required]
        public int SizeID { get; set; }
        public Size Size { get; set; }

        [Required]
        public int Quantity { get; set; }

        public string SessionId { get; set; }
    }
}