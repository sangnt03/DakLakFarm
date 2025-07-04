using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("SellerRequest")]
    public class SellerRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public DateTime RequestDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Chưa duyệt";

        [ForeignKey(nameof(UserId))]
        public virtual user User { get; set; } = null!;
    }
}
