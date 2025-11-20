using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace AgriEcommerces_MVC.Service.VnPayService
{
    public class VNPayService
    {
        private readonly IConfiguration _configuration;

        public VNPayService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Tạo URL thanh toán VNPay - 100% CHUẨN
        /// </summary>
        public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, string ipAddress)
        {
            string vnp_TmnCode = "QUUH0J2I";
            string vnp_HashSecret = "S4D0C9BVC9OGBMWBPSEOAPYWFJWWSWV3";
            string vnp_Url = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string vnp_ReturnUrl = "https://nonreadably-nontemporizing-gwendolyn.ngrok-free.dev/Payment/VNPayReturn";

            var vnTime = DateTime.UtcNow.AddHours(7);
            var createDate = vnTime.ToString("yyyyMMddHHmmss");

            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
            {
                ipAddress = "127.0.0.1";
            }

            var vnpayData = new SortedList<string, string>(new VnPayCompare());

            vnpayData.Add("vnp_Version", "2.1.0");
            vnpayData.Add("vnp_Command", "pay");
            vnpayData.Add("vnp_TmnCode", vnp_TmnCode);
            vnpayData.Add("vnp_Amount", ((long)(amount * 100)).ToString());
            vnpayData.Add("vnp_CreateDate", createDate);
            vnpayData.Add("vnp_CurrCode", "VND");
            vnpayData.Add("vnp_IpAddr", ipAddress);
            vnpayData.Add("vnp_Locale", "vn");
            vnpayData.Add("vnp_OrderInfo", $"Thanhtoandonhang{orderId}");
            vnpayData.Add("vnp_OrderType", "other");
            vnpayData.Add("vnp_ReturnUrl", vnp_ReturnUrl);
            vnpayData.Add("vnp_TxnRef", orderId.ToString());

            // ✅ THEO CHUẨN VNPAY: URL encode cả key và value
            StringBuilder data = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in vnpayData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            string queryString = data.ToString();

            // ✅ Xóa ký tự '&' cuối cùng để tạo signData
            string signData = queryString;
            if (signData.Length > 0)
            {
                signData = signData.Remove(signData.Length - 1, 1);
            }

            // ✅ Tạo hash từ signData đã encode
            string vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);

            // ✅ Tạo URL cuối cùng
            string paymentUrl = vnp_Url + "?" + queryString + "vnp_SecureHash=" + vnp_SecureHash;

            Console.WriteLine("=== VNPAY PAYMENT URL DEBUG (CHUẨN) ===");
            Console.WriteLine($"SignData: {signData}");
            Console.WriteLine($"SecureHash: {vnp_SecureHash}");
            Console.WriteLine($"PaymentUrl: {paymentUrl}");
            Console.WriteLine("========================================");

            return paymentUrl;
        }

        /// <summary>
        /// Validate chữ ký từ VNPay IPN
        /// </summary>
        public bool ValidateSignature(IQueryCollection collections, string inputHash, string secretKey)
        {
            var vnpayData = new SortedList<string, string>();

            foreach (var item in collections)
            {
                if (!string.IsNullOrEmpty(item.Key) &&
                    item.Key.StartsWith("vnp_") &&
                    item.Key != "vnp_SecureHash" &&
                    item.Key != "vnp_SecureHashType")
                {
                    vnpayData.Add(item.Key, item.Value.ToString());
                }
            }

            // Tạo chuỗi dữ liệu để hash
            var signData = string.Join("&", vnpayData.Select(kv => $"{kv.Key}={kv.Value}"));

            // Tính toán chữ ký
            var checkSum = HmacSHA512(secretKey, signData);

            return checkSum.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Mã hóa HMAC SHA512 - trả về lowercase hex string
        /// </summary>
        private string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hashValue = hmac.ComputeHash(inputBytes);
                foreach (var b in hashValue)
                {
                    hash.Append(b.ToString("x2"));
                }
            }

            return hash.ToString();
        }
        public class VnPayCompare : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                var vnpCompare = System.Globalization.CompareInfo.GetCompareInfo("en-US");
                return vnpCompare.Compare(x, y, System.Globalization.CompareOptions.Ordinal);
            }
        }

        /// <summary>
        /// Parse response code từ VNPay
        /// </summary>
        public string GetResponseDescription(string responseCode)
        {
            return responseCode switch
            {
                "00" => "Giao dịch thành công",
                "07" => "Trừ tiền thành công. Giao dịch bị nghi ngờ (liên quan tới lừa đảo, giao dịch bất thường).",
                "09" => "Thẻ/Tài khoản chưa đăng ký dịch vụ InternetBanking.",
                "10" => "Thẻ/Tài khoản không đúng hoặc chưa được kích hoạt.",
                "11" => "Đã hết hạn thanh toán.",
                "12" => "Thẻ/Tài khoản bị khóa.",
                "13" => "Sai mật khẩu xác thực giao dịch (OTP).",
                "24" => "Khách hàng hủy giao dịch.",
                "51" => "Tài khoản không đủ số dư để thanh toán.",
                "65" => "Tài khoản đã vượt quá hạn mức giao dịch trong ngày.",
                "70" => "Sai chữ ký (SecureHash không hợp lệ).",
                "75" => "Ngân hàng thanh toán đang bảo trì.",
                "79" => "KH nhập sai mật khẩu thanh toán quá số lần quy định.",
                "99" => "Lỗi không xác định",
                _ => $"Lỗi không xác định - Mã: {responseCode}"
            };
        }
    }
}