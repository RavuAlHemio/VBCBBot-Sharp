using System;
using System.Data.Common;
using System.Data.Entity;

namespace Echelon.ORM
{
    public class EchelonContext : DbContext
    {
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<Trigger> Triggers { get; set; }
        public DbSet<DictionaryIncident> DictionaryIncidents { get; set; }
        public DbSet<DictionaryTrigger> DictionaryTriggers { get; set; }

        static EchelonContext()
        {
            Database.SetInitializer<EchelonContext>(null);
        }

        public EchelonContext(DbConnection connectionToOwn) : base(connectionToOwn, true)
        {
        }
    }
}
