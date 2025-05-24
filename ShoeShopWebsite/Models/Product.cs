using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Product
    {
        public int ProductID { get; set; }

        public int CategoryID { get; set; }
        public Category? Category { get; set; }

        [Required, StringLength(255)]
        public string ProductName { get; set; }

        public string Description { get; set; }

        [Required, Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
        public ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
        public ICollection<ProductColor> ProductColors { get; set; } = new List<ProductColor>();
        public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>(); // Thêm mới
    }
}