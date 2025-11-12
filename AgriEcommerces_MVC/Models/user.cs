using System;
using System.Collections.Generic;

namespace AgriEcommerces_MVC.Models;

public partial class user
{
    public int userid { get; set; }

    public string? fullname { get; set; }

    public string email { get; set; } = null!;

    public string passwordhash { get; set; } = null!;

    public string role { get; set; } = null!;

    public string? phonenumber { get; set; }

    public bool? isapproved { get; set; }

    public DateTime? createdat { get; set; }

    public string? provider { get; set; }

    public string? shop_name { get; set; }

    public string? shop_avatar { get; set; }
    public virtual ICollection<order> orders { get; set; } = new List<order>();

    public virtual ICollection<product> products { get; set; } = new List<product>();

    public virtual ICollection<review> reviews { get; set; } = new List<review>();

    public virtual ICollection<SellerRequest> SellerRequests { get; set; } = new List<SellerRequest>();

    public virtual ICollection<promotion> promotions { get; set; } = new List<promotion>();

    public virtual ICollection<promotion_farmer> promotion_farmers { get; set; } = new List<promotion_farmer>();
    
    public virtual ICollection<promotion_usagehistory> promotion_usagehistories { get; set; } = new List<promotion_usagehistory>();

}
