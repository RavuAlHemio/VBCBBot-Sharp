using System.IO;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace VBCBBot
{
    public class Runner
    {
        protected void SetupLogging()
        {
            var confFile = new FileInfo(Path.Combine(Util.ProgramDirectory, "Logging.conf"));
            if (confFile.Exists)
            {
                XmlConfigurator.Configure(confFile);
                return;
            }

            // default config
            var rootLogger = ((Hierarchy)log4net.LogManager.GetRepository()).Root;
            rootLogger.Level = Level.Debug;
        }
    }
}
