using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    [Table("messages_on_retainer", Schema = "messenger")]
    public class MessageOnRetainer : AbstractMessage
    {
    }
}
