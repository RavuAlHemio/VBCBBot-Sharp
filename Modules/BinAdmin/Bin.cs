using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BinAdmin
{
    [Table("bins")]
    public class Bin
    {
        [Key]
        [Column("bin")]
        public string BinName { get; set; }

        public ICollection<BinItem> Items { get; set; }
    }
}
