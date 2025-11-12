using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class category
{
    public int categoryid { get; set; }

    public string categoryname { get; set; } = null!;

    public virtual ICollection<product> products { get; set; } = new List<product>();
    public virtual ICollection<promotion_category> promotion_categories { get; set; } = new List<promotion_category>();
}
