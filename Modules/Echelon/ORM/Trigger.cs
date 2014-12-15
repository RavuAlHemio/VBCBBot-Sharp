using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("triggers", Schema = "echelon")]
    public class Trigger
    {
        [Key]
        [Required]
        [Column("trigger_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("target_name_lower")]
        [MaxLength]
        public string TargetNameLower { get; set; }

        [Required]
        [Column("regex")]
        [MaxLength]
        public string Regex { get; set; }

        [Required]
        [Column("spymaster_name")]
        [MaxLength]
        public string SpymasterName { get; set; }

        public ICollection<Incident> Incidents { get; set; }
    }
}
