using System;
using System.Data.Entity;

namespace BinAdmin
{
    public class BinAdminContext : DbContext
    {
        public DbSet<Bin> Bins { get; set; }
        public DbSet<BinItem> BinItems { get; set; }

        public BinAdminContext(string contextString) : base(contextString)
        {
        }
    }
}
