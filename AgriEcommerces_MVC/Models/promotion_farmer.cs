using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("promotion_farmers")]
    public class promotion_farmer
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("promotionid")]
        public int PromotionId { get; set; }

        [Column("farmerid")]
        public int FarmerId { get; set; } // Liên kết đến Users.UserId

        // Navigation properties
        public virtual promotion Promotion { get; set; }
        public virtual user Farmer { get; set; }
    }
}