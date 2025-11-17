using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    [Table("password_resets")]
    public class password_reset
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [Column("otp_code")]
        [Required]
        [StringLength(6)]
        public string OtpCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("is_used")]
        public bool IsUsed { get; set; } = false;

        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        // Kiểm tra OTP còn hiệu lực không
        public bool IsValid()
        {
            return !IsUsed && DateTime.UtcNow < ExpiresAt;
        }
    }
}