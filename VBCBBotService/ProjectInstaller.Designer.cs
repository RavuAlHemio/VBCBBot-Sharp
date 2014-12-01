namespace VBCBBotService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.vbcbbotServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.vbcbbotServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // vbcbbotServiceProcessInstaller
            // 
            this.vbcbbotServiceProcessInstaller.Password = null;
            this.vbcbbotServiceProcessInstaller.Username = null;
            // 
            // vbcbbotServiceInstaller
            // 
            this.vbcbbotServiceInstaller.ServiceName = "VBCBBot";
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.vbcbbotServiceProcessInstaller,
            this.vbcbbotServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller vbcbbotServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller vbcbbotServiceInstaller;
    }
}