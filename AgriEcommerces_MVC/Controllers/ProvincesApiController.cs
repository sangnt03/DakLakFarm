using AgriEcommerces_MVC.Models.ApiModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Controllers
{
    // Cấu hình đây là một API Controller, route là /api/Provinces
    [Route("api/[controller]")]
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
            var response = await httpClient.GetAsync($"{ApiBaseUrl}p/");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                provinces = JsonSerializer.Deserialize<List<ProvinceApiModel>>(jsonString);

                // 3. Lưu vào Cache (ví dụ: 1 ngày)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                _memoryCache.Set(cacheKey, provinces, cacheOptions);

                return Ok(provinces);
            }

            return StatusCode((int)response.StatusCode, "Lỗi khi gọi API tỉnh thành.");
        }

        // GET: /api/Provinces/GetDistricts?provinceCode=1
        [HttpGet("GetDistricts")]
        public async Task<IActionResult> GetDistricts(int provinceCode)
        {
            string cacheKey = $"Districts_{provinceCode}";

            if (_memoryCache.TryGetValue(cacheKey, out List<DistrictApiModel> districts))
            {
                return Ok(districts);
            }

            var httpClient = _httpClientFactory.CreateClient();
            // API này trả về cả tỉnh, ta chỉ cần lấy list districts
            var response = await httpClient.GetAsync($"{ApiBaseUrl}p/{provinceCode}?depth=2");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var provinceData = JsonSerializer.Deserialize<ProvinceWithDistricts>(jsonString);
                districts = provinceData?.Districts;

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                _memoryCache.Set(cacheKey, districts, cacheOptions);

                return Ok(districts);
            }
            return StatusCode((int)response.StatusCode, "Lỗi khi gọi API quận huyện.");
        }

        // GET: /api/Provinces/GetWards?districtCode=1
        [HttpGet("GetWards")]
        public async Task<IActionResult> GetWards(int districtCode)
        {
            string cacheKey = $"Wards_{districtCode}";

            if (_memoryCache.TryGetValue(cacheKey, out List<WardApiModel> wards))
            {
                return Ok(wards);
            }

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"{ApiBaseUrl}d/{districtCode}?depth=2");

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var districtData = JsonSerializer.Deserialize<DistrictWithWards>(jsonString);
                wards = districtData?.Wards;

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(1));
                _memoryCache.Set(cacheKey, wards, cacheOptions);

                return Ok(wards);
            }
            return StatusCode((int)response.StatusCode, "Lỗi khi gọi API phường xã.");
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