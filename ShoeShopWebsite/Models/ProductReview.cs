using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShoeShopWebsite.Models {
    public class ProductReview { [Key] 
        public int ReviewID { get; set; } 
        public int ProductID { get; set; } 
        public string UserID { get; set; } 
        public int Rating { get; set; } // Điểm đánh giá (1-5)
        public string Comment { get; set; } 
        public DateTime ReviewDate { get; set; } 
        [ForeignKey("ProductID")] 
        public Product Product { get; set; } 
        [ForeignKey("UserID")] 
        public ApplicationUser User { get; set; } 
    } 
}