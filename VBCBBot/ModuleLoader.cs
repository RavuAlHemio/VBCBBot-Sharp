using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using log4net;

namespace VBCBBot
{
    public static class ModuleLoader
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static ModuleV1[] LoadModules(Config config, ChatboxConnector connector)
        {
            var ret = new List<ModuleV1>();
            foreach (var moduleConfig in config.Modules)
            {
                // load assembly
                var ass = Assembly.Load(moduleConfig.Assembly);
                if (ass == null)
                {
                    Logger.ErrorFormat("module {0}: failed to load assembly {1}", moduleConfig.ModuleClass, moduleConfig.Assembly);
                    continue;
                }

                // get the module class
                var cls = ass.GetType(moduleConfig.ModuleClass);
                if (cls == null)
                {
                    Logger.ErrorFormat("module {0}: failed to load module class", moduleConfig.ModuleClass);
                    continue;
                }

                // find the right constructor
                var ctor = cls.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new [] {typeof(ChatboxConnector), typeof(JObject)},
                    null
                );
                if (ctor == null)
                {
                    Logger.ErrorFormat("module {0}: failed to find correct constructor", moduleConfig.ModuleClass);
                    continue;
                }

                Logger.DebugFormat("loading module {0}...", moduleConfig.ModuleClass);

                // invoke it
                var moduleObject = ctor.Invoke(new object[] {connector, moduleConfig.Config});
                var module = moduleObject as ModuleV1;

                if (module == null)
                {
                    Logger.ErrorFormat("module {0}: module instance is not an instance of ModuleV1", moduleConfig.ModuleClass);

                    // attempt cleanup
                    var moduleDispo = moduleObject as IDisposable;
                    if (moduleDispo != null)
                    {
                        moduleDispo.Dispose();
                    }

                    continue;
                }

                ret.Add(module);
            }

            return ret.ToArray();
        }
    }
}

