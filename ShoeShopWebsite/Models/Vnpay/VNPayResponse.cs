namespace ShoeShopWebsite.Models.Vnpay
{
    public class VNPayResponse
    {
        public string vnp_TxnRef { get; set; }
        public string vnp_Amount { get; set; }
        public string vnp_OrderInfo { get; set; }
        public string vnp_ResponseCode { get; set; }
        public string vnp_TransactionNo { get; set; }
        public string vnp_BankCode { get; set; }
        public string vnp_PayDate { get; set; }
        public string vnp_SecureHash { get; set; }
        public bool IsValidSignature { get; set; }
    }
}