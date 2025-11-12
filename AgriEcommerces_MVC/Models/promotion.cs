using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("promotions")]
    public class promotion
    {
        [Key]
        [Column("promotionid")]
        public int PromotionId { get; set; }

        [Required]
        [Column("code")]
        [StringLength(50)]
        public string Code { get; set; }

        [Required]
        [Column("name")]
        [StringLength(200)]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Required]
        [Column("discounttype")]
        [StringLength(20)]
        public string DiscountType { get; set; } // 'percentage', 'fixed_amount', 'free_shipping'

        [Required]
        [Column("discountvalue")]
        public decimal DiscountValue { get; set; }

        [Column("maxdiscountamount")]
        public decimal? MaxDiscountAmount { get; set; }

        [Column("minordervalue")]
        public decimal MinOrderValue { get; set; } = 0;

        [Column("maxusageperuser")]
        public int? MaxUsagePerUser { get; set; }

        [Column("totalusagelimit")]
        public int? TotalUsageLimit { get; set; }

        [Column("currentusagecount")]
        public int CurrentUsageCount { get; set; } = 0;

        [Column("applicableto")]
        [StringLength(30)]
        public string ApplicableTo { get; set; } = "all"; // 'all', 'specific_products', 'specific_categories', 'specific_farmers'

        [Column("targetcustomertype")]
        [StringLength(20)]
        public string TargetCustomerType { get; set; } = "all"; // 'retail', 'wholesale', 'all'

        [Required]
        [Column("startdate")]
        public DateTime StartDate { get; set; }

        [Required]
        [Column("enddate")]
        public DateTime EndDate { get; set; }

        [Column("createdbyuserid")]
        public int? CreatedByUserId { get; set; }

        [Column("isactive")]
        public bool IsActive { get; set; } = true;

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- Navigation Properties ---
        // Liên kết với các bảng "nối"
        public virtual user CreatedBy { get; set; }
        public virtual ICollection<promotion_product> PromotionProducts { get; set; }
        public virtual ICollection<promotion_category> PromotionCategories { get; set; }
        public virtual ICollection<promotion_farmer> PromotionFarmers { get; set; }
        public virtual ICollection<promotion_usagehistory> PromotionUsageHistory { get; set; }
    }
}