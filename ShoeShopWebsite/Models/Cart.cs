using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models
{
    public class Cart
    {
        public int CartID { get; set; }
        public int ProductID { get; set; }
        public int SizeID { get; set; }
        public int? ColorID { get; set; } // Nullable để hỗ trợ trường hợp không chọn màu
        public int Quantity { get; set; }
        public string SessionId { get; set; }

        public Product Product { get; set; }
        public Size Size { get; set; }
        public Color Color { get; set; } // Quan hệ với Color
    }
}