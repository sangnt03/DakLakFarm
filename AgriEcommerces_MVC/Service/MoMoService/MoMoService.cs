using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AgriEcommerces_MVC.Service.MoMoService
{
    public class MoMoService
    {
        private readonly IConfiguration _configuration;

        public MoMoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> CreatePaymentRequest(int orderId, decimal amount, string orderInfo, string fullName)
        {
            string endpoint = _configuration["MoMo:ApiEndpoint"];
            string partnerCode = _configuration["MoMo:PartnerCode"];
            string accessKey = _configuration["MoMo:AccessKey"];
            string secretKey = _configuration["MoMo:SecretKey"];
            string returnUrl = _configuration["MoMo:ReturnUrl"];
            string notifyUrl = _configuration["MoMo:NotifyUrl"];

            string requestId = Guid.NewGuid().ToString();
            string orderIdStr = orderId.ToString() + "_" + DateTime.Now.Ticks.ToString();
            string amountStr = ((long)amount).ToString();
            string extraData = "";  

            string requestType = "captureWallet";
            string rawHash = $"accessKey={accessKey}&amount={amountStr}&extraData={extraData}&ipnUrl={notifyUrl}&orderId={orderIdStr}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={returnUrl}&requestId={requestId}&requestType=captureWallet";

            string signature = ComputeHmacSha256(rawHash, secretKey);

            var message = new
            {
                partnerCode = partnerCode,
                partnerName = "AgriStore",
                storeId = "AgriStore",
                requestId = requestId,
                amount = amountStr,
                orderId = orderIdStr,
                orderInfo = orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = notifyUrl,
                lang = "vi",
                extraData = extraData,
                requestType = requestType,
                signature = signature
            };

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(endpoint, new StringContent(JsonConvert.SerializeObject(message), Encoding.UTF8, "application/json"));
                var responseContent = await response.Content.ReadAsStringAsync();

                // --- DEBUG LOGGING (Xem lỗi ở đây) ---
                var jsonResponse = JObject.Parse(responseContent);
                Console.WriteLine("--- MOMO RESPONSE ---");
                Console.WriteLine(responseContent);
                // -------------------------------------

                if (jsonResponse["resultCode"]?.ToString() != "0")
                {
                    // Nếu lỗi, throw exception để hiện ra trình duyệt cho dễ sửa
                    string errorMsg = jsonResponse["message"]?.ToString() ?? "Lỗi không xác định từ MoMo";
                    throw new Exception($"MoMo Error: {errorMsg} (LocalMessage: {jsonResponse["localMessage"]})");
                }

                return jsonResponse["payUrl"]?.ToString();
            }
        }

        // Hàm xác thực chữ ký khi MoMo trả kết quả về (IPN & Return)
        public bool ValidateSignature(IQueryCollection collection, string providedSignature)
        {
            string accessKey = _configuration["MoMo:AccessKey"];
            string secretKey = _configuration["MoMo:SecretKey"];

            // Lấy các tham số từ URL
            string partnerCode = collection["partnerCode"];
            string orderId = collection["orderId"];
            string requestId = collection["requestId"];
            string amount = collection["amount"];
            string orderInfo = collection["orderInfo"];
            string orderType = collection["orderType"];
            string transId = collection["transId"];
            string resultCode = collection["resultCode"];
            string message = collection["message"];
            string payType = collection["payType"];
            string responseTime = collection["responseTime"];
            string extraData = collection["extraData"];

            // Tạo chuỗi hash theo đúng format của MoMo Response
            string rawHash = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&message={message}&orderId={orderId}&orderInfo={orderInfo}&orderType={orderType}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={responseTime}&resultCode={resultCode}&transId={transId}";

            string calculatedSignature = ComputeHmacSha256(rawHash, secretKey);

            return calculatedSignature.Equals(providedSignature, StringComparison.OrdinalIgnoreCase);
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            byte[] keyByte = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }
    }
}