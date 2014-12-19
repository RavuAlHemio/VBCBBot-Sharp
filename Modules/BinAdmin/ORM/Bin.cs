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
        [Required]
        [Column("bin_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("bin_name", Order = 2)]
        [MaxLength(-1)]
        public string BinName { get; set; }

        public ICollection<BinItem> Items { get; set; }
    }
}
