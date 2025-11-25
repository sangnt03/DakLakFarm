using System;
using System.Collections.Generic;
using System.Linq;
using AgriEcommerces_MVC.Service.ShipService;

namespace AgriEcommerces_MVC.Service
{
    public class ShippingService : IShippingService
    {
        // Địa chỉ gốc: Dak Lak
        private const string BASE_PROVINCE = "Đắk Lắk";

        // Bảng phí ship theo khu vực
        private readonly Dictionary<string, decimal> _shippingRates = new()
        {
            // Miền Trung (gần Dak Lak)
            { "Đắk Lắk", 0 }, // Cùng tỉnh: miễn phí
            { "Đắk Nông", 30000 },
            { "Gia Lai", 30000 },
            { "Kon Tum", 30000 },
            { "Lâm Đồng", 30000 },
            { "Khánh Hòa", 30000 },
            { "Phú Yên", 30000 },
            { "Bình Định", 30000 },
            { "Quảng Ngãi", 30000 },
            { "Quảng Nam", 30000 },
            { "Đà Nẵng", 30000 },
            { "Quảng Trị", 30000 },
            { "Thừa Thiên Huế", 30000 },
            { "Quảng Bình", 30000 },
            { "Hà Tĩnh", 30000 },
            { "Nghệ An", 30000 },
            { "Thanh Hóa", 30000 },
            { "Ninh Thuận", 30000 },
            { "Bình Thuận", 30000 },

            // Miền Bắc
            { "Hà Nội", 40000 },
            { "Hải Phòng", 40000 },
            { "Hải Dương", 40000 },
            { "Hưng Yên", 40000 },
            { "Bắc Ninh", 40000 },
            { "Bắc Giang", 40000 },
            { "Thái Nguyên", 40000 },
            { "Quảng Ninh", 40000 },
            { "Lạng Sơn", 40000 },
            { "Cao Bằng", 40000 },
            { "Bắc Kạn", 40000 },
            { "Tuyên Quang", 40000 },
            { "Lào Cai", 40000 },
            { "Yên Bái", 40000 },
            { "Phú Thọ", 40000 },
            { "Vĩnh Phúc", 40000 },
            { "Hà Giang", 40000 },
            { "Điện Biên", 40000 },
            { "Lai Châu", 40000 },
            { "Sơn La", 40000 },
            { "Hòa Bình", 40000 },
            { "Ninh Bình", 40000 },
            { "Nam Định", 40000 },
            { "Thái Bình", 40000 },

            // Miền Nam
            { "Hồ Chí Minh", 40000 },
            { "Bình Dương", 40000 },
            { "Đồng Nai", 40000 },
            { "Bà Rịa - Vũng Tàu", 40000 },
            { "Tây Ninh", 40000 },
            { "Bình Phước", 40000 },
            { "Long An", 40000 },
            { "Tiền Giang", 40000 },
            { "Bến Tre", 40000 },
            { "Vĩnh Long", 40000 },
            { "Trà Vinh", 40000 },
            { "Đồng Tháp", 40000 },
            { "An Giang", 40000 },
            { "Kiên Giang", 40000 },
            { "Cần Thơ", 40000 },
            { "Hậu Giang", 40000 },
            { "Sóc Trăng", 40000 },
            { "Bạc Liêu", 40000 },
            { "Cà Mau", 40000 }
        };

        public decimal CalculateShippingFee(string provinceCity)
        {
            if (string.IsNullOrWhiteSpace(provinceCity))
            {
                return 30000; // Phí mặc định nếu không có thông tin
            }

            // Chuẩn hóa tên tỉnh (loại bỏ khoảng trắng thừa, chuyển về dạng tiêu chuẩn)
            string normalizedProvince = NormalizeProvinceName(provinceCity);

            // Tìm kiếm chính xác
            if (_shippingRates.TryGetValue(normalizedProvince, out decimal fee))
            {
                return fee;
            }

            // Tìm kiếm gần đúng (chứa chuỗi con)
            var matchingProvince = _shippingRates.Keys
                .FirstOrDefault(k => normalizedProvince.Contains(k) || k.Contains(normalizedProvince));

            if (matchingProvince != null)
            {
                return _shippingRates[matchingProvince];
            }

            // Phí mặc định nếu không tìm thấy
            return 50000;
        }

        private string NormalizeProvinceName(string provinceName)
        {
            if (string.IsNullOrWhiteSpace(provinceName))
                return string.Empty;

            // Loại bỏ các tiền tố thường gặp
            string[] prefixes = { "Tỉnh", "Thành phố", "TP.", "TP" };
            string normalized = provinceName.Trim();

            foreach (var prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(prefix.Length).Trim();
                    break;
                }
            }

            return normalized;
        }

        /// <summary>
        /// Tính phí ship theo khoảng cách ước tính (km)
        /// Phương thức này có thể được mở rộng để tích hợp với Google Maps API
        /// </summary>
        public decimal CalculateShippingFeeByDistance(double distanceKm)
        {
            const decimal BASE_FEE = 15000;
            const decimal RATE_PER_KM = 500; // 500đ/km
            const decimal MAX_FEE = 100000;

            decimal calculatedFee = BASE_FEE + (decimal)(distanceKm * (double)RATE_PER_KM);

            return Math.Min(calculatedFee, MAX_FEE);
        }
    }
}