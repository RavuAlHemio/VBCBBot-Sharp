using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("dict_incidents", Schema = "echelon")]
    public class DictionaryIncident
    {
        [Key]
        [Required]
        [Column("dict_incident_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }

        [Required]
        [Column("dict_trigger_id", Order = 2)]
        [ForeignKey("Trigger")]
        public long TriggerID { get; set; }

        public virtual DictionaryTrigger Trigger { get; set; }

        [Required]
        [Column("message_id", Order = 3)]
        public long MessageID { get; set; }

        [Required]
        [Column("timestamp", Order = 4)]
        public DateTime Timestamp { get; set; }

        [Required]
        [Column("perpetrator_name", Order = 5)]
        public string PerpetratorName { get; set; }

        [Required]
        [Column("original_word", Order = 6)]
        public string OriginalWord { get; set; }

        [Required]
        [Column("corrected_word", Order = 7)]
        public string CorrectedWord { get; set; }

        [Required]
        [Column("expunged", Order = 8)]
        [DefaultValue(false)]
        public bool Expunged { get; set; }
    }
}
