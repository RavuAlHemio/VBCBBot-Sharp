using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    [Table("ignore_list", Schema = "messenger")]
    public class IgnoreEntry
    {
        [Key]
        [Required]
        [Column("sender_folded", Order = 1)]
        [MaxLength(255)]
        public string SenderFolded { get; set; }

        [Key]
        [Required]
        [Column("recipient_folded", Order = 2)]
        [MaxLength(255)]
        public string RecipientFolded { get; set; }
    }
}
