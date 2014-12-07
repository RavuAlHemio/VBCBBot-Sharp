using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BinAdmin
{
    [Table("bin_items")]
    public class BinItem
    {
        [Key]
        [ForeignKey("BinName")]
        [Column("bin")]
        public Bin Bin { get; set; }

        [Key]
        [Column("item")]
        public string Item { get; set; }

        [Column("arrow")]
        public string Arrow { get; set; }

        [Column("thrower")]
        public string Thrower { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}
