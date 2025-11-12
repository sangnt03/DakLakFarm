using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models;

public partial class order
{
    public int orderid { get; set; }

    public int customerid { get; set; }

    public DateTime? orderdate { get; set; }

    public decimal totalamount { get; set; }

    public string status { get; set; } = null!;

    public string? shippingaddress { get; set; }

    public string customername { get; set; }
    public string customerphone { get; set; }
    public virtual user customer { get; set; } = null!;

    public virtual ICollection<orderdetail> orderdetails { get; set; } = new List<orderdetail>();
    public int? promotionid { get; set; }
    public decimal discountamount { get; set; } 

    [ForeignKey("promotionid")]
    public virtual promotion? promotion { get; set; }
    public virtual ICollection<promotion_usagehistory> promotion_usagehistories { get; set; } = new List<promotion_usagehistory>();
}
