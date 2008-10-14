using System;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.ConnectionDlg;
using Microsoft.SqlServer.Management.UI.VSIntegration;

namespace InternalsViewer.SSMSAddIn
{
    public partial class AllocationContainer : UserControl
    {
        private WindowManager windowManager;

        internal WindowManager WindowManager
        {
            get { return windowManager; }
            set { windowManager = value; }
        }

        public AllocationContainer()
        {
            InitializeComponent();

            this.allocationWindowControl.Connect += new EventHandler(AllocationWindow_Connect);
            this.allocationWindowControl.ViewPage += new EventHandler<PageEventArgs>(AllocationWindowControl_ViewPage);
        }

        void AllocationWindowControl_ViewPage(object sender, PageEventArgs e)
        {
            windowManager.CreatePageViewerWindow(e.Address);
        }

        void AllocationWindow_Connect(object sender, EventArgs e)
        {
            using (ShellConnectionDialog dialog = new ShellConnectionDialog())
            {
                IServerType serverType = new SqlServerType();
                
                dialog.AddServer(serverType);

                UIConnectionInfo connectionInfo = new UIConnectionInfo();
               
                if (dialog.ShowDialogCollectValues(ServiceCache.MainShellWindow, ref connectionInfo) == DialogResult.OK)
                {
                    ConnectionManager.ConnectInternalsViewer(connectionInfo);

                    this.allocationWindowControl.RefreshDatabases();
                }
            }

        }
    }
}
