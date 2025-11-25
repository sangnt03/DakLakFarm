namespace AgriEcommerces_MVC.Service.ShipService
{
    public interface IShippingService
    {
        decimal CalculateShippingFee(string provinceCity);
    }
}
