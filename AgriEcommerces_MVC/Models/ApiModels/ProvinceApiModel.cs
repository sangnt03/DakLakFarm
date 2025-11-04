using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Models.ApiModels
{
    public class ProvinceApiModel
    {
        public string Name { get; set; }
        public int Code { get; set; }
        public string Codename { get; set; } 
        public string DivisionType { get; set; } 
        public int PhoneCode { get; set; }
        public List<DistrictApiModel> Districts { get; set; }
    }
}