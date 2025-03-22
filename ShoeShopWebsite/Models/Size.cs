using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Size
    {
        public int SizeID { get; set; }

        [Required, StringLength(10)]
        public string SizeName { get; set; }
    }
}
