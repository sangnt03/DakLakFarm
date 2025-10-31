using System.Text.Json.Serialization;

namespace AgriEcommerces_MVC.Models.ApiModels
{
    public class DistrictApiModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }
}