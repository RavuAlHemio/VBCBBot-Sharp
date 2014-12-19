using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BinAdmin.ORM
{
    [Table("bin_items", Schema = "bin_admin")]
    public class BinItem
    {
        [Key]
        [Required]
        [Column("bin_item_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("bin_id")]
        [ForeignKey("Bin")]
        public long BinId { get; set; }

        public virtual Bin Bin { get; set; }

        [Required]
        [Column("item")]
        [MaxLength(-1)]
        public string Item { get; set; }

        [Required]
        [Column("arrow")]
        [MaxLength(-1)]
        public string Arrow { get; set; }

        [Required]
        [Column("thrower")]
        [MaxLength(-1)]
        public string Thrower { get; set; }

        [Required]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
