using System;
using System.Data.Common;
using System.Data.Entity;

namespace Stfu.ORM
{
    public class StfuContext : DbContext
    {
        public DbSet<Ban> Bans { get; set; }

        static StfuContext()
        {
            Database.SetInitializer<StfuContext>(null);
        }

        public StfuContext(DbConnection connectionToOwn) : base(connectionToOwn, true)
        {
        }
    }
}
