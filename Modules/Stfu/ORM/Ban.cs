using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stfu.ORM
{
    [Table("bans", Schema = "stfu")]
    public class Ban
    {
        [Key]
        [Required]
        [Column("banned_user", Order = 1)]
        public string BannedUser { get; set; }

        [Column("deadline", Order = 2)]
        public DateTime? Deadline { get; set; }

        [Required]
        [Column("banner", Order = 3)]
        public string Banner { get; set; }
    }
}

