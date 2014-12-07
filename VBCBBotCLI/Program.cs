using System.IO;
using System.Text;
using VBCBBot;

namespace VBCBBotCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            // load configuration
            Config config;
            var configPath = Path.Combine(Util.ProgramDirectory, "Config.json");
            using (var reader = new StreamReader(configPath, Encoding.UTF8))
            {
                config = new Config(reader.ReadToEnd());
            }

            // initialize HTML decompiler and chatbox connector
            var decompiler = new HtmlDecompiler(config.HtmlDecompiler.SmileyUrlToSymbol, config.Forum.TeXPrefix);
            var connector = new ChatboxConnector(config.Forum, decompiler);

            // load modules
            ModuleLoader.LoadModules(config, connector);

            // launch connector
            connector.Start();
        }
    }
}
