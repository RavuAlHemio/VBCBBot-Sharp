﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stfu.ORM
{
    [Table("bans", Schema = "stfu")]
    public class Ban
    {
        [Key]
        [Column("banned_user")]
        public string BannedUser { get; set; }

        [Column("deadline")]
        public DateTime? Deadline { get; set; }

        [Column("banner")]
        public string Banner { get; set; }
    }
}
