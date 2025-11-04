using AgriEcommerces_MVC.Models.ApiModels;
using Microsoft.AspNetCore.Hosting; // Thêm cho IWebHostEnvironment
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Controllers
{
    [Route("api/Provinces")]
    [ApiController]
    public class ProvincesApiController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IWebHostEnvironment _environment; // Thêm để lấy path file
        private const string CacheKey = "ProvincesFullData"; // Cache key cho toàn bộ dữ liệu

        public ProvincesApiController(IMemoryCache memoryCache, IWebHostEnvironment environment)
        {
            _memoryCache = memoryCache;
            _environment = environment;
        }

        // Phương thức private để load và cache dữ liệu từ JSON file
        private List<ProvinceApiModel> GetCachedProvinces()
        {
            if (_memoryCache.TryGetValue(CacheKey, out List<ProvinceApiModel> provinces))
            {
                return provinces;
            }

            try
            {
                // Path đến file (điều chỉnh nếu cần)
                var filePath = Path.Combine(_environment.WebRootPath, "data", "provinces.json");

                if (!System.IO.File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File provinces.json không tồn tại tại {filePath}");
                }

                var jsonString = System.IO.File.ReadAllText(filePath);
                provinces = JsonSerializer.Deserialize<List<ProvinceApiModel>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Cache vĩnh viễn vì dữ liệu tĩnh (hoặc set expiration nếu cần)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromDays(365)); // Cache 1 năm
                _memoryCache.Set(CacheKey, provinces, cacheOptions);

                return provinces ?? new List<ProvinceApiModel>();
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần (sử dụng ILogger nếu có)
                Console.WriteLine($"Lỗi load provinces.json: {ex.Message}");
                return new List<ProvinceApiModel>();
            }
        }

        // GET: /api/Provinces (chỉ return list provinces: name và code)
        [HttpGet]
        public IActionResult GetProvinces()
        {
            var fullData = GetCachedProvinces();
            if (fullData.Count == 0)
            {
                return StatusCode(500, "Lỗi load dữ liệu tỉnh thành từ file.");
            }

            // Chỉ return name và code để khớp code cũ
            var provinces = fullData.Select(p => new { p.Name, p.Code }).ToList();
            return Ok(provinces);
        }

        // GET: /api/Provinces/GetDistricts?provinceCode=1
        [HttpGet("GetDistricts")]
        public IActionResult GetDistricts([FromQuery] int provinceCode)
        {
            if (provinceCode <= 0)
            {
                return BadRequest("Mã tỉnh không hợp lệ");
            }

            var fullData = GetCachedProvinces();
            var province = fullData.FirstOrDefault(p => p.Code == provinceCode);

            if (province == null || province.Districts == null || province.Districts.Count == 0)
            {
                return NotFound("Không tìm thấy quận/huyện cho tỉnh này.");
            }

            // Return districts: name và code
            var districts = province.Districts.Select(d => new { d.Name, d.Code }).ToList();
            return Ok(districts);
        }

        // GET: /api/Provinces/GetWards?districtCode=1
        [HttpGet("GetWards")]
        public IActionResult GetWards([FromQuery] int districtCode)
        {
            if (districtCode <= 0)
            {
                return BadRequest("Mã quận không hợp lệ");
            }

            var fullData = GetCachedProvinces();
            var district = fullData.SelectMany(p => p.Districts).FirstOrDefault(d => d.Code == districtCode);

            if (district == null || district.Wards == null || district.Wards.Count == 0)
            {
                return NotFound("Không tìm thấy phường/xã cho quận này.");
            }

            // Return wards: name và code
            var wards = district.Wards.Select(w => new { w.Name, w.Code }).ToList();
            return Ok(wards);
        }
    }
}