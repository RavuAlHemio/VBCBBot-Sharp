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
        [Column("bin_item_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("bin_id", Order = 2)]
        [ForeignKey("Bin")]
        public long BinId { get; set; }

        public virtual Bin Bin { get; set; }

        [Required]
        [Column("item", Order = 3)]
        public string Item { get; set; }

        [Required]
        [Column("arrow", Order = 4)]
        public string Arrow { get; set; }

        [Required]
        [Column("thrower", Order = 5)]
        public string Thrower { get; set; }

        [Required]
        [Column("timestamp", Order = 6)]
        public DateTime Timestamp { get; set; }
    }
}
