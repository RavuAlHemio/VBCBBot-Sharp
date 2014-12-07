using System;

namespace VBCBBot
{
    public interface IDatabaseModuleConfig
    {
        string DatabaseProvider { get; }
        string DatabaseConnectionString { get; }
    }
}

