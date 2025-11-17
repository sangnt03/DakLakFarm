using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Models
{
    [Table("order_cancellations")]
    public class order_cancellation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("orderid")]
        public int OrderId { get; set; }

        [Column("cancelled_by")]
        public int CancelledBy { get; set; } // User ID người hủy

        [Column("cancel_reason")]
        [StringLength(500)]
        public string? CancelReason { get; set; }

        [Column("cancelled_at")]
        public DateTime CancelledAt { get; set; } // timestamp without time zone

        [Column("refund_amount")]
        [Precision(12, 2)]
        public decimal? RefundAmount { get; set; }

        [Column("refund_status")]
        [StringLength(50)]
        public string? RefundStatus { get; set; } // "Pending", "Completed", "N/A"

        // Navigation properties
        [ForeignKey("OrderId")]
        public virtual order? Order { get; set; }

        [ForeignKey("CancelledBy")]
        public virtual user? CancelledByUser { get; set; }
    }
}