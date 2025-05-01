using ShoeShopWebsite.Controllers.Libraries;
using ShoeShopWebsite.Models;
using ShoeShopWebsite.Models.Vnpay;
using ShoeShopWebsite.Services.VnPay;

public class VnPayService : IVNPayService
{
    private readonly IConfiguration _configuration;
    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(PaymentInformationModel model, HttpContext context)
    {
        // Kiểm tra Amount hợp lệ
        if (model.Amount < 0)
        {
            throw new ArgumentException("Số tiền không được âm.");
        }

        // Kiểm tra giới hạn của long sau khi nhân 100
        decimal amountAfterMultiply = model.Amount * 100;
        if (amountAfterMultiply > long.MaxValue)
        {
            throw new ArgumentException($"Số tiền sau khi nhân 100 ({amountAfterMultiply}) vượt quá giới hạn của kiểu long ({long.MaxValue}).");
        }

        var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(_configuration["TimeZoneId"]);
        var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);
        var tick = DateTime.Now.Ticks.ToString(); // Sử dụng số hóa đơn (ticks) thay vì OrderId
        var pay = new VnpayLibrary();
        var urlCallBack = _configuration["Vnpay:PaymentBackReturnUrl"];

        // Log các tham số cấu hình
        Console.WriteLine($"[VNPayService] Config: Version={_configuration["Vnpay:Version"]}, Command={_configuration["Vnpay:Command"]}, TmnCode={_configuration["Vnpay:TmnCode"]}, CurrCode={_configuration["Vnpay:CurrCode"]}, Locale={_configuration["Vnpay:Locale"]}, BaseUrl={_configuration["Vnpay:BaseUrl"]}, ReturnUrl={urlCallBack}");

        pay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
        pay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
        pay.AddRequestData("vnp_TmnCode", _configuration["Vnpay:TmnCode"]);
        var vnpAmount = (long)(model.Amount * 100);
        pay.AddRequestData("vnp_Amount", vnpAmount.ToString()); // Amount là decimal, nhân 100 và ép sang long
        pay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
        pay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
        pay.AddRequestData("vnp_IpAddr", pay.GetIpAddress(context));
        pay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);
        pay.AddRequestData("vnp_OrderInfo", model.OrderInfo); // Sử dụng OrderInfo từ model
        pay.AddRequestData("vnp_OrderType", model.OrderType);
        pay.AddRequestData("vnp_ReturnUrl", urlCallBack);
        pay.AddRequestData("vnp_TxnRef", tick); // Sử dụng số hóa đơn

        Console.WriteLine($"[VNPayService] Request Data: vnp_Amount={vnpAmount}, vnp_OrderInfo={model.OrderInfo}, vnp_TxnRef={tick}");

        var paymentUrl = pay.CreateRequestUrl(_configuration["Vnpay:BaseUrl"], _configuration["Vnpay:HashSecret"]);
        return paymentUrl;
    }

    public PaymentResponseModel PaymentExecute(IQueryCollection collections)
    {
        var pay = new VnpayLibrary();
        var response = pay.GetFullResponseData(collections, _configuration["Vnpay:HashSecret"]);
        return response;
    }
}