using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    [Table("replayable_messages", Schema = "messenger")]
    public class ReplayableMessage : AbstractMessage
    {
    }
}
