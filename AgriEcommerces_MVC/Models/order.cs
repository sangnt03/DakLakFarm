using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models;

public partial class order
{
    public int orderid { get; set; }

    // MÃ ĐỌN HÀNG ĐẶC BIỆT - Hiển thị cho khách hàng
    [StringLength(50)]
    [Column("ordercode")]
    public string ordercode { get; set; }

    public int customerid { get; set; }
    public DateTime? orderdate { get; set; }

    // totalamount = Tạm tính (Subtotal)
    public decimal totalamount { get; set; }
    public string status { get; set; } = null!;
    public string? shippingaddress { get; set; }
    public string customername { get; set; }
    public string customerphone { get; set; }

    // --- BỔ SUNG CÁC TRƯỜNG KHUYẾN MÃI ---

    // Số tiền đã giảm
    public decimal discountamount { get; set; }

    // Tiền cuối cùng phải trả
    // Ánh xạ 'FinalAmount' (C#) tới cột 'finalamount' (CSDL)
    [Column("finalamount", TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    // Mã code đã sử dụng
    // Ánh xạ 'PromotionCode' (C#) tới cột 'promotioncode' (CSDL)
    [Column("promotioncode")]
    [StringLength(50)]
    public string? PromotionCode { get; set; }

    // ID khuyến mãi
    public int? promotionid { get; set; }

    [ForeignKey("promotionid")]
    public virtual promotion? promotion { get; set; }
    // --- (Kết thúc bổ sung) ---

    public virtual user customer { get; set; } = null!;
    public virtual ICollection<orderdetail> orderdetails { get; set; } = new List<orderdetail>();
    public virtual ICollection<promotion_usagehistory> promotion_usagehistories { get; set; } = new List<promotion_usagehistory>();
}