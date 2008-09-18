using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.UI.ConnectionDlg;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.VSIntegration;

namespace InternalsViewer.SSMSAddIn
{
    public partial class AllocationWindow : UserControl
    {
        public AllocationWindow()
        {
            InitializeComponent();

            this.allocationWindowControl.Connect += new EventHandler(AllocationWindow_Connect);
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
