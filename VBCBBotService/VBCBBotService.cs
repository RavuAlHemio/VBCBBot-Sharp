using System.ServiceProcess;

namespace VBCBBotService
{
    public partial class VBCBBotService : ServiceBase
    {
        public VBCBBotService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }
    }
}
