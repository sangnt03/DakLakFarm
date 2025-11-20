using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("payoutrequests")]
    public class PayoutRequest
    {
        [Key]
        [Column("payoutrequestid")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PayoutRequestId { get; set; }

        [Column("farmerid")]
        public int FarmerId { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        [StringLength(30)]
        public string Status { get; set; } // "Pending", "Completed", "Rejected"

        [Column("bankdetails",TypeName ="jsonb")]
        public string BankDetails { get; set; } // Lưu trữ dưới dạng chuỗi JSON

        [Column("requestdate")]
        public DateTime RequestDate { get; set; }

        [Column("completeddate")]
        public DateTime? CompletedDate { get; set; } // Nullable

        // --- Navigation Properties ---

        [ForeignKey("FarmerId")]
        public virtual user Farmer { get; set; }
    }
}
