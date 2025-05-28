using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeShopWebsite.Models 
{
    public class ProductReview
    {
        [Key]
        public int ReviewID { get; set; } // Primary key
        public int ProductID { get; set; }
        public int OrderID { get; set; }
        public string UserID { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime ReviewDate { get; set; }
        public int? SizeID { get; set; }
        public int? ColorID { get; set; }

        // Navigation properties
        public Product Product { get; set; }
        public Order Order { get; set; }
        public Size? Size { get; set; }
        public Color? Color { get; set; }
        public ApplicationUser User { get; set; }
    }
}