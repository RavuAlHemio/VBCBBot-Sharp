using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BinAdmin.ORM
{
    [Table("bin_items", Schema = "bin_admin")]
    public class BinItem
    {
        [Key]
        [Column("bin_item_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("bin_id")]
        public long BinId { get; set; }

        [ForeignKey("BinId")]
        public virtual Bin Bin { get; set; }

        [Column("item")]
        [MaxLength]
        public string Item { get; set; }

        [Column("arrow")]
        [MaxLength]
        public string Arrow { get; set; }

        [Column("thrower")]
        [MaxLength]
        public string Thrower { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
