using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("promotion_categories")]
    public class promotion_category
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("promotionid")]
        public int PromotionId { get; set; }

        [Column("categoryid")]
        public int CategoryId { get; set; }

        // Navigation properties
        public virtual promotion Promotion { get; set; }
        public virtual category Category { get; set; }
    }
}