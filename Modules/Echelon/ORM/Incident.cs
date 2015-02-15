using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("incidents", Schema = "echelon")]
    public class Incident
    {
        [Key]
        [Required]
        [Column("incident_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("trigger_id", Order = 2)]
        [ForeignKey("Trigger")]
        public long TriggerId { get; set; }

        public virtual Trigger Trigger { get; set; }

        [Required]
        [Column("message_id", Order = 3)]
        public long MessageId { get; set; }

        [Required]
        [Column("timestamp", Order = 4)]
        public DateTime Timestamp { get; set; }

        [Required]
        [Column("perpetrator_name", Order = 5)]
        public string PerpetratorName { get; set; }
    }
}
