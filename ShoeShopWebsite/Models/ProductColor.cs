using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class ProductColor
    {
        [Key]
        public int ProductColorID { get; set; }

        [ForeignKey("Product")]
        public int ProductID { get; set; }
        public Product Product { get; set; }

        [ForeignKey("Color")]
        public int ColorID { get; set; }
        public Color Color { get; set; }
    }
}
