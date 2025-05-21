using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class priceprediction
{
    public int predictionid { get; set; }

    public int productid { get; set; }

    public DateOnly predictedmonth { get; set; }

    public decimal? predictedprice { get; set; }

    public decimal? temperature { get; set; }

    public decimal? rainfall { get; set; }

    public int? harvestvolume { get; set; }

    public DateTime? createdat { get; set; }

    public virtual product product { get; set; } = null!;
}
