using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("payments")]
    public class Payment
    {
        [Key]
        [Column("paymentid")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PaymentId { get; set; }

        [Column("orderid")]
        public int OrderId { get; set; }

        [Column("paymentmethod")]
        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        [StringLength(30)]
        public string Status { get; set; }

        [Column("gatewaytransactioncode")]
        [StringLength(100)]
        public string? GatewayTransactionCode { get; set; } // Có thể null

        [Column("createdate")]
        public DateTime CreateDate { get; set; }

        // --- Navigation Properties ---

        [ForeignKey("OrderId")]
        public virtual order order { get; set; }
    }
}
