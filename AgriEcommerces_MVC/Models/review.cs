using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class review
{
    public int reviewid { get; set; }

    public int productid { get; set; }

    public int customerid { get; set; }

    public short rating { get; set; }

    public string? comment { get; set; }

    public DateTime? createdat { get; set; }

    public virtual user customer { get; set; } = null!;

    public virtual product product { get; set; } = null!;
}
