using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Color
    {
        [Key]
        public int ColorID { get; set; }

        [Required]
        [StringLength(50)]
        public string ColorName { get; set; }
    }
}
