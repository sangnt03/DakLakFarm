using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("promotion_usagehistory")]
    public class promotion_usagehistory
    {
        [Column("usageid")]
        public int UsageId { get; set; }

        [Column("promotionid")]
        public int PromotionId { get; set; }

        [Column("userid")]
        public int UserId { get; set; }

        [Column("orderid")]
        public int OrderId { get; set; }

        [Column("discountamount")]
        public decimal DiscountAmount { get; set; }

        [Column("usedat")]
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual promotion Promotion { get; set; }
        public virtual user User { get; set; }
        public virtual order Order { get; set; }
    }
}