using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    public abstract class AbstractMessage
    {
        [Key]
        [Required]
        [Column("message_id")]
        public long ID { get; set; }

        [Required]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [Column("sender_original")]
        [MaxLength(255)]
        public string SenderOriginal { get; set; }

        [Required]
        [Column("recipient_folded")]
        [MaxLength(255)]
        public string RecipientFolded { get; set; }

        [Required]
        [Column("body")]
        [MaxLength(-1)]
        public string Body { get; set; }

        public AbstractMessage()
        {
        }

        public AbstractMessage(AbstractMessage other)
        {
            this.ID = other.ID;
            this.Timestamp = other.Timestamp;
            this.SenderOriginal = other.SenderOriginal;
            this.RecipientFolded = other.RecipientFolded;
            this.Body = other.Body;
        }
    }
}
