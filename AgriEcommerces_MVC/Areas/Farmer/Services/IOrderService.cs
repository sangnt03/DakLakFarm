using System.Collections.Generic;
using System.Threading.Tasks;
using AgriEcommerces_MVC.Areas.Farmer.ViewModel;
using AgriEcommerces_MVC.Areas.Farmer.ViewModels;

namespace AgriEcommerces_MVC.Areas.Farmer.Services
{
    public interface IOrderService
    {
        Task<List<RevenusViewModel>> GetRevenueAsync(int farmerId, int year, int? month = null);

    }
}
