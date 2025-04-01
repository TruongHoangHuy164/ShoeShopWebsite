using Microsoft.Extensions.Configuration;
using ShoeShopWebsite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShoeShopWebsite.Services
{
    public class VNPayService : IVNPayService
    {
        private readonly IConfiguration _configuration;

        public VNPayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreatePaymentUrl(Order order, string ipAddress)
        {
            var vnp_TmnCode = _configuration["VNPay:vnp_TmnCode"];
            var vnp_HashSecret = _configuration["VNPay:vnp_HashSecret"];
            var vnp_Url = _configuration["VNPay:vnp_Url"];
            var vnp_ReturnUrl = _configuration["VNPay:vnp_ReturnUrl"];

            var vnpayRequest = new VNPayRequest
            {
                vnp_Version = "2.1.0",
                vnp_Command = "pay",
                vnp_TmnCode = vnp_TmnCode,
                vnp_Amount = (long)(order.TotalPrice * 100),
                vnp_CreateDate = DateTime.Now.ToString("yyyyMMddHHmmss"),
                vnp_CurrCode = "VND",
                vnp_IpAddr = ipAddress,
                vnp_Locale = "vn",
                vnp_OrderInfo = $"Thanh toan don hang #{order.OrderID}",
                vnp_OrderType = "250000",
                vnp_ReturnUrl = vnp_ReturnUrl,
                vnp_TxnRef = order.OrderID.ToString() + "_" + DateTime.Now.Ticks
            };

            var sortedParams = new SortedDictionary<string, string>
            {
                { "vnp_Amount", vnpayRequest.vnp_Amount.ToString() },
                { "vnp_Command", vnpayRequest.vnp_Command },
                { "vnp_CreateDate", vnpayRequest.vnp_CreateDate },
                { "vnp_CurrCode", vnpayRequest.vnp_CurrCode },
                { "vnp_IpAddr", vnpayRequest.vnp_IpAddr },
                { "vnp_Locale", vnpayRequest.vnp_Locale },
                { "vnp_OrderInfo", Uri.EscapeDataString(vnpayRequest.vnp_OrderInfo) },
                { "vnp_OrderType", vnpayRequest.vnp_OrderType },
                { "vnp_ReturnUrl", vnpayRequest.vnp_ReturnUrl },
                { "vnp_TmnCode", vnpayRequest.vnp_TmnCode },
                { "vnp_TxnRef", vnpayRequest.vnp_TxnRef },
                { "vnp_Version", vnpayRequest.vnp_Version }
            };

            var signData = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            vnpayRequest.vnp_SecureHash = ComputeHmacSha512(signData, vnp_HashSecret);

            var paymentUrl = $"{vnp_Url}?{ToQueryString(vnpayRequest)}";
            Console.WriteLine($"Sign Data (CreatePaymentUrl): {signData}");
            Console.WriteLine($"Secure Hash (CreatePaymentUrl): {vnpayRequest.vnp_SecureHash}");
            return paymentUrl;
        }

        public VNPayResponse PaymentExecute(IQueryCollection query)
        {
            var response = new VNPayResponse
            {
                vnp_TxnRef = query["vnp_TxnRef"],
                vnp_Amount = query["vnp_Amount"],
                vnp_OrderInfo = query["vnp_OrderInfo"],
                vnp_ResponseCode = query["vnp_ResponseCode"],
                vnp_TransactionNo = query["vnp_TransactionNo"],
                vnp_BankCode = query["vnp_BankCode"],
                vnp_PayDate = query["vnp_PayDate"],
                vnp_SecureHash = query["vnp_SecureHash"]
            };
            return response;
        }

        private string ComputeHmacSha512(string message, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var hash = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        string IVNPayService.ComputeHmacSha512(string message, string key)
        {
            throw new NotImplementedException();
        }

        private string ToQueryString(VNPayRequest request)
        {
            var properties = from p in request.GetType().GetProperties()
                             where p.GetValue(request, null) != null
                             select p.Name + "=" + Uri.EscapeDataString(p.GetValue(request, null).ToString());
            return string.Join("&", properties);
        }
    }
}