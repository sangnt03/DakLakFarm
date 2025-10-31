using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ApiModels; // Model cho API (ProvinceApiModel)
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json; // Cần cho GetFromJsonAsync
using System.Security.Claims;

namespace AgriEcommerces_MVC.Controllers
{
    [Authorize] // Yêu cầu người dùng phải đăng nhập
    public class CustomerAddressController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory; // Tiêm HttpClientFactory

        // Cập nhật Constructor
        public CustomerAddressController(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory; // Gán
        }

        // Hàm trợ giúp để lấy ID người dùng hiện tại
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return -1; // Sẽ bị chặn bởi [Authorize]
        }

        // Hàm trợ giúp: Hủy tất cả mặc định CŨ
        private async Task ClearAllDefaults(int userId)
        {
            await _db.customer_addresses
                .Where(a => a.user_id == userId && a.is_default)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.is_default, false));
        }

        // HÀM MỚI: Lấy danh sách tỉnh ban đầu từ API
        // (Chỉ gọi 1 lần khi tải trang Create/Edit)
        private async Task<List<ProvinceApiModel>> GetProvincesListAsync()
        {
            var httpClient = _httpClientFactory.CreateClient();
            try
            {
                // Gọi thẳng API công cộng
                // (Giả định ProvincesApiController của bạn dùng cache cho việc này,
                // nhưng gọi thẳng từ đây cũng không sao vì chỉ là danh sách tỉnh)
                var provinces = await httpClient
                    .GetFromJsonAsync<List<ProvinceApiModel>>("https://provinces.open-api.vn/api/p/");

                return provinces ?? new List<ProvinceApiModel>();
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                Console.WriteLine($"Lỗi khi gọi API tỉnh thành: {ex.Message}");
                return new List<ProvinceApiModel>(); // Trả về list rỗng nếu lỗi
            }
        }


        // GET: /CustomerAddress
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var addresses = await _db.customer_addresses
                .Where(a => a.user_id == userId)
                .OrderByDescending(a => a.is_default) // Luôn ưu tiên mặc định lên đầu
                .ToListAsync();

            return View(addresses);
        }

        // GET: /CustomerAddress/Create
        public async Task<IActionResult> Create()
        {
            var vm = new AddressViewModel();

            // Lấy danh sách tỉnh từ API và gửi sang View
            ViewBag.Provinces = await GetProvincesListAsync();

            return View(vm);
        }

        // POST: /CustomerAddress/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddressViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Nếu model không hợp lệ, phải tải lại danh sách tỉnh cho View
                ViewBag.Provinces = await GetProvincesListAsync();
                return View(vm);
            }

            int userId = GetCurrentUserId();

            // Logic xử lý "Mặc định"
            if (vm.IsDefault)
            {
                await ClearAllDefaults(userId);
            }

            var newAddress = new customer_address
            {
                user_id = userId,
                recipient_name = vm.RecipientName,
                phone_number = vm.PhoneNumber,
                full_address = vm.FullAddress,
                // Dữ liệu này là string TÊN, được điền bởi JavaScript
                province_city = vm.ProvinceCity,
                district = vm.District,
                ward_commune = vm.WardCommune,
                is_default = vm.IsDefault,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            _db.customer_addresses.Add(newAddress);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm địa chỉ mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /CustomerAddress/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            int userId = GetCurrentUserId();
            var address = await _db.customer_addresses
                                 .FirstOrDefaultAsync(a => a.id == id && a.user_id == userId);

            if (address == null)
            {
                return NotFound();
            }

            var vm = new AddressViewModel
            {
                Id = address.id,
                RecipientName = address.recipient_name,
                PhoneNumber = address.phone_number,
                FullAddress = address.full_address,
                ProvinceCity = address.province_city,
                District = address.district,
                WardCommune = address.ward_commune,
                IsDefault = address.is_default
            };

            // Lấy danh sách tỉnh từ API và gửi sang View
            ViewBag.Provinces = await GetProvincesListAsync();

            return View(vm);
        }

        // POST: /CustomerAddress/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AddressViewModel vm)
        {
            if (id != vm.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                // Nếu model không hợp lệ, phải tải lại danh sách tỉnh cho View
                ViewBag.Provinces = await GetProvincesListAsync();
                return View(vm);
            }

            int userId = GetCurrentUserId();
            var addressInDb = await _db.customer_addresses
                                    .FirstOrDefaultAsync(a => a.id == id && a.user_id == userId);

            if (addressInDb == null)
            {
                return NotFound();
            }

            // Logic xử lý "Mặc định"
            if (vm.IsDefault)
            {
                await ClearAllDefaults(userId);
            }

            // Cập nhật thông tin
            addressInDb.recipient_name = vm.RecipientName;
            addressInDb.phone_number = vm.PhoneNumber;
            addressInDb.full_address = vm.FullAddress;
            addressInDb.province_city = vm.ProvinceCity;
            addressInDb.district = vm.District;
            addressInDb.ward_commune = vm.WardCommune;
            addressInDb.is_default = vm.IsDefault;
            addressInDb.updated_at = DateTime.UtcNow;

            _db.customer_addresses.Update(addressInDb);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật địa chỉ thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /CustomerAddress/SetDefault/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id)
        {
            int userId = GetCurrentUserId();

            await using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Hủy tất cả mặc định cũ
                    await ClearAllDefaults(userId);

                    // 2. Đặt mặc định mới
                    var addressToSet = await _db.customer_addresses
                                        .FirstOrDefaultAsync(a => a.id == id && a.user_id == userId);

                    if (addressToSet == null)
                    {
                        return NotFound();
                    }

                    addressToSet.is_default = true;
                    addressToSet.updated_at = DateTime.UtcNow;
                    _db.customer_addresses.Update(addressToSet);

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi đặt địa chỉ mặc định.";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = "Đã đặt địa chỉ mặc định.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /CustomerAddress/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var address = await _db.customer_addresses
                                 .FirstOrDefaultAsync(a => a.id == id && a.user_id == userId);

            if (address == null)
            {
                return NotFound();
            }

            return View(address);
        }

        // POST: /CustomerAddress/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int userId = GetCurrentUserId();
            var address = await _db.customer_addresses
                                .FirstOrDefaultAsync(a => a.id == id && a.user_id == userId);

            if (address == null)
            {
                return NotFound();
            }

            if (address.is_default)
            {
                TempData["ErrorMessage"] = "Không thể xóa địa chỉ mặc định. Vui lòng chọn địa chỉ khác làm mặc định trước.";
                return RedirectToAction(nameof(Index));
            }

            _db.customer_addresses.Remove(address);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa địa chỉ thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}