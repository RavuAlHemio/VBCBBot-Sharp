using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BinAdmin.ORM
{
    [Table("bins", Schema = "bin_admin")]
    public class Bin
    {
        [Key]
        [Column("bin_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("bin_name")]
        [MaxLength]
        public string BinName { get; set; }

        public ICollection<BinItem> Items { get; set; }
    }
}
