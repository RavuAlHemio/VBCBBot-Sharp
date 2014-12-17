using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    [Table("messages", Schema = "messenger")]
    public class Message : AbstractMessage
    {
    }
}
