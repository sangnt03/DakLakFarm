using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class product
{
    public int productid { get; set; }

    public int userid { get; set; }

    public int categoryid { get; set; }

    public string productname { get; set; } = null!;

    public string? description { get; set; }

    public string? unit { get; set; }

    public decimal price { get; set; }

    public int quantityavailable { get; set; }

    public DateTime? createdat { get; set; }

    public virtual category category { get; set; } = null!;

    public virtual ICollection<historicalpricedatum> historicalpricedata { get; set; } = new List<historicalpricedatum>();

    public virtual ICollection<orderdetail> orderdetails { get; set; } = new List<orderdetail>();

    public virtual ICollection<priceprediction> pricepredictions { get; set; } = new List<priceprediction>();

    public virtual ICollection<productimage> productimages { get; set; } = new List<productimage>();

    public virtual ICollection<review> reviews { get; set; } = new List<review>();

    public virtual ICollection<promotion_product> promotion_products { get; set; } = new List<promotion_product>();

    public virtual user user { get; set; } = null!;
}
