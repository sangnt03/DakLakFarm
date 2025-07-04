

using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Areas.Farmer.ViewModel
{
    public class DuDoanViewModel
    {
        public List<GiaNongSan> Predictions { get; set; }
        public List<DateTime> FutureDates { get; set; }
        public string SelectedDate { get; set; }
    }

    public class GiaNongSan
    {
        public DateTime Ngay { get; set; }
        public string Loai { get; set; }
        public string KhuVuc { get; set; }
        public double Gia { get; set; }  // hoặc decimal nếu dùng kiểu tiền tệ
        public string DonVi { get; set; }
    }
}


