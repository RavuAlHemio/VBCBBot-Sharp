using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.ORM
{
    [Table("messages", Schema = "messenger")]
    public class Message : AbstractMessage
    {
        public Message()
            : base()
        {
        }

        public Message(AbstractMessage other)
            : base(other)
        {
        }
    }
}
