﻿using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echelon.ORM
{
    [Table("triggers", Schema = "echelon")]
    public class Trigger
    {
        [Key]
        [Required]
        [Column("trigger_id", Order = 1)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("target_name_lower", Order = 2)]
        public string TargetNameLower { get; set; }

        [Required]
        [Column("regex", Order = 3)]
        public string Regex { get; set; }

        [Required]
        [Column("spymaster_name", Order = 4)]
        public string SpymasterName { get; set; }

        [Required]
        [Column("deactivated", Order = 5)]
        [DefaultValue(false)]
        public bool Deactivated { get; set; }

        public ICollection<Incident> Incidents { get; set; }
    }
}
