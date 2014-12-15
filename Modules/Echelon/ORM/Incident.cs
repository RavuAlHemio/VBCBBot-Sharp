using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("incidents", Schema = "echelon")]
    public class Incident
    {
        [Key]
        [Required]
        [Column("incident_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("trigger_id")]
        [ForeignKey("Trigger")]
        public long TriggerId { get; set; }

        public virtual Trigger Trigger { get; set; }

        [Required]
        [Column("message_id")]
        public long MessageId { get; set; }

        [Required]
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }

        [Required]
        [Column("perpetrator_name")]
        [MaxLength]
        public string PerpetratorName { get; set; }
    }
}
