using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("promotion_products")]
    public class promotion_product
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("promotionid")]
        public int PromotionId { get; set; }

        [Column("productid")]
        public int ProductId { get; set; }

        // Navigation properties
        public virtual promotion Promotion { get; set; }
        public virtual product Product { get; set; }
    }
}