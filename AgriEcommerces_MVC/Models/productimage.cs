using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class productimage
{
    public int imageid { get; set; }

    public int productid { get; set; }

    public string imageurl { get; set; } = null!;

    public DateTime? uploadedat { get; set; }

    public virtual product product { get; set; } = null!;
}
