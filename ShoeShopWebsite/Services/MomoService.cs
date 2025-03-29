using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ShoeShopWebsite.Models;
using System.Security.Cryptography;
using System.Text;

namespace ShoeShopWebsite.Services
{
    public class MomoService : IMomoService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public MomoService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<MomoResponse> CreatePaymentAsync(Order order)
        {
            var partnerCode = _configuration["MomoAPI:PartnerCode"];
            var accessKey = _configuration["MomoAPI:AccessKey"];
            var secretKey = _configuration["MomoAPI:SecretKey"];
            var requestId = DateTime.Now.Ticks.ToString();
            var orderId = order.OrderID.ToString();
            var orderInfo = $"Thanh toán đơn hàng #{order.OrderID}";
            var redirectUrl = _configuration["MomoAPI:ReturnUrl"];
            var ipnUrl = _configuration["MomoAPI:NotifyUrl"];
            var requestType = _configuration["MomoAPI:RequestType"];
            var amount = (long)order.TotalPrice;

            // Tạo raw signature
            var rawData = $"accessKey={accessKey}&amount={amount}&extraData=&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";
            var signature = ComputeHmacSha256(rawData, secretKey);

            var request = new MomoRequest
            {
                partnerCode = partnerCode,
                accessKey = accessKey,
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = redirectUrl,
                ipnUrl = ipnUrl,
                requestType = requestType,
                extraData = "",
                signature = signature
            };

            var client = _httpClientFactory.CreateClient();
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_configuration["MomoAPI:MomoApiUrl"], content);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MomoResponse>(responseString);
        }

        public MomoCallback PaymentExecuteAsync(IQueryCollection query)
        {
            var callback = new MomoCallback
            {
                partnerCode = query["partnerCode"],
                orderId = query["orderId"],
                requestId = query["requestId"],
                amount = long.Parse(query["amount"]),
                orderInfo = query["orderInfo"],
                orderType = query["orderType"],
                transId = long.Parse(query["transId"]),
                resultCode = int.Parse(query["resultCode"]),
                message = query["message"],
                signature = query["signature"]
            };
            return callback;
        }

        private string ComputeHmacSha256(string message, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var hash = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}