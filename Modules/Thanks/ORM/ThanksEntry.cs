using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Thanks.ORM
{
    [Table("thanks", Schema = "thanks")]
    public class ThanksEntry
    {
        [Key]
        [Required]
        [Column("thanker", Order = 0)]
        [MaxLength(255)]
        public string Thanker { get; set; }

        [Key]
        [Required]
        [Column("thankee_folded", Order = 1)]
        [MaxLength(255)]
        public string ThankeeFolded { get; set; }

        [Required]
        [Column("thank_count")]
        public int ThankCount { get; set; }
    }
}

