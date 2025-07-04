// Areas/Farmer/Services/OrderService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;
using AgriEcommerces_MVC.Areas.Farmer.ViewModels;
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
            // Câu query cơ bản
            var baseQuery = _db.orderdetails
                .Where(od =>
                    od.sellerid == farmerId &&
                    od.order.orderdate.HasValue &&
                    od.order.orderdate.Value.Year == year
                );

            if (month.HasValue)
            {
                // CHỈ tháng được chọn: nhóm theo đúng tháng đó
                var single = await baseQuery
                    .Where(od => od.order.orderdate.Value.Month == month.Value)
                    .GroupBy(od => od.order.orderdate.Value.Month)
                    .Select(g => new { Month = g.Key, Total = g.Sum(x => x.quantity * x.unitprice) })
                    .FirstOrDefaultAsync();

                return new List<RevenusViewModel>
                {
                    new RevenusViewModel {
                        Period       = new DateTime(year, month.Value, 1),
                        TotalRevenue = single?.Total ?? 0m
                    }
                };
            }
            else
            {
                // TẤT CẢ: luôn phát sinh 12 tháng
                var rawList = await baseQuery
                    .GroupBy(od => od.order.orderdate.Value.Month)
                    .Select(g => new { Month = g.Key, Total = g.Sum(x => x.quantity * x.unitprice) })
                    .ToListAsync();

                // Đảm bảo 12 tháng, tháng không có data = 0
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
