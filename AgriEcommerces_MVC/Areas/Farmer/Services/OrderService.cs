using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;
using AgriEcommerces_MVC.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Areas.Farmer.Services
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _db;
        public OrderService(ApplicationDbContext db) => _db = db;

        public async Task<List<RevenusViewModel>> GetRevenueAsync(
            int farmerId,
            int year,
            int? month = null
        )
        {
            var baseQuery = _db.orderdetails
                .Where(od =>
                    od.sellerid == farmerId &&
                    od.order.orderdate.HasValue &&
                    od.order.orderdate.Value.Year == year &&
                    od.order.status == "Delivered"
                );
                   

            if (month.HasValue)
            {
                var dailyData = await baseQuery
                .Where(od => od.order.orderdate.Value.Month == month.Value)
                .GroupBy(od => od.order.orderdate.Value.Day)
                .Select(g => new {
                    Day = g.Key,
                    Total = g.Sum(x => x.FarmerRevenue)
                })
                .ToListAsync();

                int daysInMonth = DateTime.DaysInMonth(year, month.Value);

                var result = Enumerable.Range(1, daysInMonth)
                    .Select(day => {
                        var found = dailyData.FirstOrDefault(x => x.Day == day);
                        return new RevenusViewModel
                        {
                            // Period sẽ là ngày cụ thể: 01/11/2025, 02/11/2025...
                            Period = new DateTime(year, month.Value, day),
                            TotalRevenue = found?.Total ?? 0m
                        };
                    })
                    .ToList();

                return result;
            }
            else
            {
                var rawList = await baseQuery
                    .GroupBy(od => od.order.orderdate.Value.Month)
                    .Select(g => new {
                        Month = g.Key,
                        // 2. SỬA CÁCH TÍNH TỔNG TƯƠNG TỰ Ở ĐÂY
                        Total = g.Sum(x => x.FarmerRevenue)
                    })
                    .ToListAsync();

                var result = Enumerable.Range(1, 12)
                    .Select(m => {
                        var found = rawList.FirstOrDefault(r => r.Month == m);
                        return new RevenusViewModel
                        {
                            Period = new DateTime(year, m, 1),
                            TotalRevenue = found?.Total ?? 0m
                        };
                    })
                    .ToList();

                return result;
            }
        }
    }
}