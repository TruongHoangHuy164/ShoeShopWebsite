using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ShoeShopWebsite.Models.Vnpay
{
    public class VnpayModel
    {
        [Key]
        public int Id { get; set; }
        public string OrderDescription { get; set; }
        public string TransactionId { get; set; }
        public string OrderId { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentId { get; set; }
        public DateTime DateCreated { get; set; }
        //lỗi thì xóa :P
        [NotMapped]
        public double TotalPrice { get; set; }
    }
}
