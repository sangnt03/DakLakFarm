using Microsoft.AspNetCore.Mvc;
using AgriEcommerces_MVC.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;
using Microsoft.AspNetCore.Authorization;

namespace AgriEcommerces_MVC.Areas.Farmer.Controllers
{
    [Area("Farmer")]
    [Authorize(AuthenticationSchemes = "FarmerAuth", Roles = "Farmer")]
    public class KetQuaDuDoanController : Controller
    {
        private readonly string _connectionString;

        public KetQuaDuDoanController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet]
        public async Task<IActionResult> Index(string predictDate)
        {
            var futureDates = GetFutureDates();
            var viewModel = new DuDoanViewModel
            {
                FutureDates = futureDates,
                Predictions = new List<GiaNongSan>(),
                SelectedDate = predictDate
            };

            if (string.IsNullOrEmpty(predictDate))
            {
                // Nếu không chọn ngày -> lấy dữ liệu thực tế ngày mới nhất trong bảng gia_nong_san
                var latestDate = await GetLatestActualDate();
                var actualData = await GetActualDataForDate(latestDate);

                viewModel.SelectedDate = latestDate.ToString("yyyy-MM-dd");
                viewModel.Predictions = actualData;
                ViewBag.SuccessMessage = $"Hiển thị dữ liệu thực tế ngày {latestDate:dd/MM/yyyy}.";
                return View(viewModel);
            }

            // Nếu đã chọn ngày, kiểm tra định dạng ngày
            if (!DateTime.TryParse(predictDate, out DateTime parsedDate))
            {
                ViewBag.ErrorMessage = "Ngày dự đoán không hợp lệ.";
                return View(viewModel);
            }

            var predictions = await GetPredictionsForDate(parsedDate);
            viewModel.Predictions = predictions;

            if (predictions == null || predictions.Count == 0)
            {
                ViewBag.ErrorMessage = $"Không có dữ liệu dự đoán cho ngày {parsedDate:dd/MM/yyyy}.";
            }
            else
            {
                ViewBag.SuccessMessage = $"Tìm thấy {predictions.Count} dự đoán cho ngày {parsedDate:dd/MM/yyyy}.";
            }

            return View(viewModel);
        }

        private List<DateTime> GetFutureDates()
        {
            var futureDates = new List<DateTime>();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT MAX(ngay) FROM gia_nong_san", conn))
                    {
                        var lastDate = cmd.ExecuteScalar() as DateTime? ?? DateTime.Today;
                        for (int i = 1; i <= 30; i++) // chỉ lấy 5 ngày tiếp theo
                        {
                            futureDates.Add(lastDate.AddDays(i));
                        }
                    }
                }
            }
            catch
            {
                futureDates = new List<DateTime> { DateTime.Today.AddDays(1) };
            }

            return futureDates;
        }

        private async Task<DateTime> GetLatestActualDate()
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT MAX(ngay) FROM gia_nong_san", conn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        return result as DateTime? ?? DateTime.Today;
                    }
                }
            }
            catch
            {
                return DateTime.Today;
            }
        }

        private async Task<List<GiaNongSan>> GetActualDataForDate(DateTime date)
        {
            var data = new List<GiaNongSan>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new NpgsqlCommand("SELECT ngay, loai, khu_vuc, gia, don_vi FROM gia_nong_san WHERE ngay::date = @date", conn))
                    {
                        cmd.Parameters.AddWithValue("date", date.Date);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new GiaNongSan
                                {
                                    Ngay = reader.GetDateTime(0),
                                    Loai = reader.GetString(1),
                                    KhuVuc = reader.GetString(2),
                                    Gia = reader.GetDouble(3),
                                    DonVi = reader.GetString(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi lấy dữ liệu thực tế: {ex.Message}";
            }

            return data;
        }

        private async Task<List<GiaNongSan>> GetPredictionsForDate(DateTime date)
        {
            var predictions = new List<GiaNongSan>();

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    using (var cmd = new NpgsqlCommand("SELECT ngay, loai, khu_vuc, gia, don_vi FROM du_doan_gia_nong_san WHERE ngay::date = @predictDate", conn))
                    {
                        cmd.Parameters.AddWithValue("predictDate", date.Date);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                predictions.Add(new GiaNongSan
                                {
                                    Ngay = reader.GetDateTime(0),
                                    Loai = reader.GetString(1),
                                    KhuVuc = reader.GetString(2),
                                    Gia = reader.GetDouble(3),
                                    DonVi = reader.GetString(4)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi khi truy vấn dữ liệu dự đoán: {ex.Message}";
            }

            return predictions;
        }
    }
}
