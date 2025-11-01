using AgriEcommerces_MVC.Models.ApiModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Controllers
{
    // FIX: Đổi route thành "Provinces" thay vì "[controller]"
    [Route("api/Provinces")]
    [ApiController]
    public class ProvincesApiController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private const string ApiBaseUrl = "https://provinces.open-api.vn/api/";

        public ProvincesApiController(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
        }

        // GET: /api/Provinces
        [HttpGet]
        public async Task<IActionResult> GetProvinces()
        {
            const string cacheKey = "ProvincesList";

            // 1. Thử lấy từ Cache
            if (_memoryCache.TryGetValue(cacheKey, out List<ProvinceApiModel> provinces))
            {
                return Ok(provinces);
            }

            // 2. Nếu không có cache, gọi API
            var httpClient = _httpClientFactory.CreateClient();

            try
            {
                var response = await httpClient.GetAsync($"{ApiBaseUrl}p/");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    provinces = JsonSerializer.Deserialize<List<ProvinceApiModel>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // 3. Lưu vào Cache (1 ngày)
                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                    _memoryCache.Set(cacheKey, provinces, cacheOptions);

                    return Ok(provinces);
                }

                return StatusCode((int)response.StatusCode, "Lỗi khi gọi API tỉnh thành.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        // GET: /api/Provinces/GetDistricts?provinceCode=1
        [HttpGet("GetDistricts")]
        public async Task<IActionResult> GetDistricts([FromQuery] int provinceCode)
        {
            if (provinceCode <= 0)
            {
                return BadRequest("Mã tỉnh không hợp lệ");
            }

            string cacheKey = $"Districts_{provinceCode}";

            if (_memoryCache.TryGetValue(cacheKey, out List<DistrictApiModel> districts))
            {
                return Ok(districts);
            }

            var httpClient = _httpClientFactory.CreateClient();

            try
            {
                var response = await httpClient.GetAsync($"{ApiBaseUrl}p/{provinceCode}?depth=2");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var provinceData = JsonSerializer.Deserialize<ProvinceWithDistricts>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    districts = provinceData?.Districts ?? new List<DistrictApiModel>();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                    _memoryCache.Set(cacheKey, districts, cacheOptions);

                    return Ok(districts);
                }

                return StatusCode((int)response.StatusCode, "Lỗi khi gọi API quận huyện.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        // GET: /api/Provinces/GetWards?districtCode=1
        [HttpGet("GetWards")]
        public async Task<IActionResult> GetWards([FromQuery] int districtCode)
        {
            if (districtCode <= 0)
            {
                return BadRequest("Mã quận không hợp lệ");
            }

            string cacheKey = $"Wards_{districtCode}";

            if (_memoryCache.TryGetValue(cacheKey, out List<WardApiModel> wards))
            {
                return Ok(wards);
            }

            var httpClient = _httpClientFactory.CreateClient();

            try
            {
                var response = await httpClient.GetAsync($"{ApiBaseUrl}d/{districtCode}?depth=2");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var districtData = JsonSerializer.Deserialize<DistrictWithWards>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    wards = districtData?.Wards ?? new List<WardApiModel>();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                    _memoryCache.Set(cacheKey, wards, cacheOptions);

                    return Ok(wards);
                }

                return StatusCode((int)response.StatusCode, "Lỗi khi gọi API phường xã.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

        // Các class trợ giúp để Deserialize API
        private class ProvinceWithDistricts
        {
            [JsonPropertyName("districts")]
            public List<DistrictApiModel> Districts { get; set; }
        }

        private class DistrictWithWards
        {
            [JsonPropertyName("wards")]
            public List<WardApiModel> Wards { get; set; }
        }
    }
}