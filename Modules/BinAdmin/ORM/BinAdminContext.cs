using System;
using System.Data.Common;
using System.Data.Entity;

namespace BinAdmin.ORM
{
    public class BinAdminContext : DbContext
    {
        public DbSet<Bin> Bins { get; set; }
        public DbSet<BinItem> BinItems { get; set; }

        static BinAdminContext()
        {
            Database.SetInitializer<BinAdminContext>(null);
        }

        public BinAdminContext(DbConnection connectionToOwn) : base(connectionToOwn, true)
        {
        }
    }
}
