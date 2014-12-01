using System.ServiceProcess;

namespace VBCBBotService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            var servicesToRun = new ServiceBase[] 
            { 
                new VBCBBotService() 
            };
            ServiceBase.Run(servicesToRun);
        }
    }
}
