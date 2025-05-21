using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class historicalpricedatum
{
    public int dataid { get; set; }

    public int productid { get; set; }

    public DateOnly datamonth { get; set; }

    public decimal actualprice { get; set; }

    public decimal? temperature { get; set; }

    public decimal? rainfall { get; set; }

    public int? harvestvolume { get; set; }

    public DateTime? createdat { get; set; }

    public virtual product product { get; set; } = null!;
}
