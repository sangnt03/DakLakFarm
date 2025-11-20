using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models;

public partial class orderdetail
{
    public int orderdetailid { get; set; }

    public int orderid { get; set; }

    public int productid { get; set; }

    public int quantity { get; set; }

    public decimal unitprice { get; set; }

    public int sellerid { get; set; }

    [Column("admincommission")]
    public decimal AdminCommission { get; set; }

    [Column("farmerrevenue")]
    public decimal FarmerRevenue { get; set; }
    public virtual order order { get; set; } = null!;

    public virtual product product { get; set; } = null!;
    public virtual user seller { get; set; }

    [NotMapped]
    public decimal SumPrice => quantity * unitprice;


}
