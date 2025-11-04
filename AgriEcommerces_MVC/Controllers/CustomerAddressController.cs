using AgriEcommerces_MVC.Data;
using AgriEcommerces_MVC.Models;
using AgriEcommerces_MVC.Models.ApiModels;
using AgriEcommerces_MVC.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace AgriEcommerces_MVC.Controllers
{
    [Authorize]
    public class CustomerAddressController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public CustomerAddressController(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        // Lấy ID người dùng hiện tại
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return -1; // Bị chặn bởi [Authorize]
        }

        // Xóa tất cả địa chỉ mặc định cũ
        private async Task ClearAllDefaults(int userId)
        {
            await _db.customer_addresses
                .Where(a => a.user_id == userId && a.is_default)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.is_default, false));
        }

        private async Task<List<ProvinceApiModel>> GetProvincesListAsync()
        {
            var httpClient = _httpClientFactory.CreateClient();

            // QUAN TRỌNG: Thiết lập BaseAddress
            httpClient.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

            try
            {
                var response = await httpClient.GetAsync("/api/Provinces");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<ProvinceApiModel>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<ProvinceApiModel>();
                }

                Console.WriteLine($"Lỗi API nội bộ: {response.StatusCode}");
                return new List<ProvinceApiModel>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gọi API tỉnh thành: {ex.Message}");
                return new List<ProvinceApiModel>();
            }
        }

        // GET: /CustomerAddress
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var addresses = await _db.customer_addresses
                .Where(a => a.user_id == userId)
                .OrderByDescending(a => a.is_default)
                .ToListAsync();

            return View(addresses);
        }

        // GET: /CustomerAddress/Create
        public async Task<IActionResult> Create()
        {
            var vm = new AddressViewModel();
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
                ViewBag.Provinces = await GetProvincesListAsync();
                return View(vm);
            }

            int userId = GetCurrentUserId();

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

            if (vm.IsDefault)
            {
                await ClearAllDefaults(userId);
            }

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
                    await ClearAllDefaults(userId);

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