using System.ComponentModel.DataAnnotations;
using ShoeShopWebsite.Models;

public class OrderDetail
{
    [Key]  // Định nghĩa khóa chính
    public int DetailID { get; set; }

    [Required]
    public int OrderID { get; set; }
    public Order Order { get; set; }

    [Required]
    public int ProductID { get; set; }
    public Product Product { get; set; }

    [Required]
    public int SizeID { get; set; }
    public Size Size { get; set; }
    public Color Color { get; set; } // Quan hệ với Color

    [Required]
    public int Quantity { get; set; }

    [Required]
    public decimal Price { get; set; }
    public int? ColorID { get; set; }
}
