using System;
using System.Data.Entity;

namespace VBCBBot
{
    public static class EntityFrameworkExtensions
    {
        public static void DeleteAll<T>(this DbContext context)
            where T : class
        {
            foreach (var p in context.Set<T>())
            {
                context.Entry(p).State = EntityState.Deleted;
            }
        }
    }
}
