using System.IO;
using System.Text;
using VBCBBot;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace VBCBBotCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // log to console
            var repo = (Hierarchy)LogManager.GetRepository();
            repo.Root.Level = Level.Debug;
            repo.Configured = true;
            var layout = new PatternLayout
            {
                ConversionPattern = "%-6timestamp [%15.15thread] %-5level %30.30logger %ndc - %message%newline"
            };
            layout.ActivateOptions();
            var conApp = new ConsoleAppender
            {
                Layout = layout
            };
            conApp.ActivateOptions();
            repo.Root.AddAppender(conApp);

            // load configuration
            Config config;
            var configPath = Path.Combine(Util.ProgramDirectory, "Config.json");
            using (var reader = new StreamReader(configPath, Encoding.UTF8))
            {
                config = new Config(reader.ReadToEnd());
            }

            // initialize HTML decompiler and chatbox connector
            var decompiler = new HtmlDecompiler(config.HtmlDecompiler.SmileyUrlToSymbol, config.HtmlDecompiler.TeXPrefix);
            var connector = new ChatboxConnector(config.Forum, decompiler);

            // load modules
            ModuleLoader.LoadModules(config, connector);

            // launch connector
            connector.Start();
        }
    }
}
