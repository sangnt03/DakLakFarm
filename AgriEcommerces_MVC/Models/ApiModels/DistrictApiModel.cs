using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Models.ApiModels
{
    public class DistrictApiModel
    {
        public string Name { get; set; }
        public int Code { get; set; }
        public string Codename { get; set; }
        public string DivisionType { get; set; }
        public string ShortCodename { get; set; }
        public List<WardApiModel> Wards { get; set; } 
    }
}