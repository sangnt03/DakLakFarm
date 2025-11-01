using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriEcommerces_MVC.Models
{
    // Chúng ta đặt tên class là "customer_address" (số ít, chữ thường)
    // để khớp với convention model "user" mà bạn đang dùng.
    // Data Annotation [Table] sẽ ánh xạ class này tới bảng "customer_addresses" (số nhiều) trong CSDL.
    [Table("customer_addresses")]
    public class customer_address
    {
        [Key] // Đánh dấu đây là khóa chính
        [Column("id")] // Ánh xạ tới cột 'id'
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Column("user_id")]
        [ForeignKey("User")] // Tên của Navigation Property "User" ở cuối file
        public int user_id { get; set; }

        [Column("recipient_name")]
        [StringLength(255)] // Giới hạn độ dài giống như CSDL
        public string recipient_name { get; set; }

        [Column("phone_number")]
        [StringLength(20)]
        public string phone_number { get; set; }

        [Column("full_address", TypeName = "text")]
        public string full_address { get; set; }

        [Column("province_city")]
        [StringLength(100)]
        public string province_city { get; set; }

        [Column("district")]
        [StringLength(100)]
        public string district { get; set; }

        [Column("ward_commune")]
        [StringLength(100)]
        public string ward_commune { get; set; }

        [Column("is_default")]
        public bool is_default { get; set; }

        [Column("created_at")]
        public DateTime created_at { get; set; }

        [Column("updated_at")]
        public DateTime updated_at { get; set; }

        
        public virtual user User { get; set; }
    }
}