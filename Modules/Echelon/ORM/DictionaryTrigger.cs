using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("dict_triggers", Schema = "echelon")]
    public class DictionaryTrigger
    {
        [Key]
        [Required]
        [Column("dict_trigger_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ID { get; set; }

        [Required]
        [Column("orig_string", Order = 2)]
        public string OriginalString { get; set; }

        [Required]
        [Column("repl_string", Order = 3)]
        public string ReplacementString { get; set; }

        [Required]
        [Column("word_list", Order = 4)]
        public string WordList { get; set; }

        [Required]
        [Column("spymaster_name", Order = 5)]
        public string SpymasterName { get; set; }

        [Required]
        [Column("deactivated", Order = 6)]
        [DefaultValue(false)]
        public bool Deactivated { get; set; }

        public ICollection<DictionaryIncident> Incidents { get; set; }
    }
}
