using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("wallettransactions")]
    public class WalletTransaction
    {
        [Key]
        [Column("transactionid")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long TransactionId { get; set; } // Dùng long vì là bigint

        [Column("farmerid")]
        public int FarmerId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; } // Âm là trừ tiền, dương là cộng tiền

        [Column("type")]
        [StringLength(50)]
        public string Type { get; set; } // "OrderRevenue", "Payout", "Refund"

        [Column("referenceid")]
        public int? ReferenceId { get; set; } // ID của OrderDetail hoặc PayoutRequest

        [Column("description")]
        public string? Description { get; set; }

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        // --- Navigation Properties ---

        // Liên kết với Farmer
        [ForeignKey("FarmerId")]
        public virtual user Farmer { get; set; }
    }
}
