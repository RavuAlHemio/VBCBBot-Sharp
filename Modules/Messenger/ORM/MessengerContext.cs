using System;
using System.Data.Common;
using System.Data.Entity;

namespace Messenger.ORM
{
    public class MessengerContext : DbContext
    {
        public DbSet<Message> Messages { get; set; }
        public DbSet<MessageOnRetainer> MessagesOnRetainer { get; set; }
        public DbSet<ReplayableMessage> ReplayableMessages { get; set; }
        public DbSet<IgnoreEntry> IgnoreList { get; set; }

        static MessengerContext()
        {
            Database.SetInitializer<MessengerContext>(null);
        }

        public MessengerContext(DbConnection connectionToOwn) : base(connectionToOwn, true)
        {
        }
    }
}
