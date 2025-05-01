namespace ShoeShopWebsite.Models.Vnpay
{
    public class PaymentInformationModel
    {
        /// <summary>
        /// Mã đơn hàng (bắt buộc)
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// Số tiền thanh toán (bắt buộc, đơn vị VND * 100, phải là số nguyên)
        /// Ví dụ: 3,063,199 VND thì gán 306319900
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Thông tin đơn hàng (bắt buộc, mô tả giao dịch)
        /// </summary>
        public string OrderInfo { get; set; }

        /// <summary>
        /// Loại đơn hàng (tùy chọn, mặc định "250000" cho sản phẩm/dịch vụ thông thường)
        /// </summary>
        public string OrderType { get; set; } = "250000";

        /// <summary>
        /// Mã ngân hàng (tùy chọn, để trống nếu không chọn ngân hàng cụ thể)
        /// </summary>
        public string BankCode { get; set; } = "";

        // Thuộc tính không cần thiết cho VNPay, có thể bỏ
        public string OrderDescription { get; set; }
        public string Name { get; set; }
    }
}