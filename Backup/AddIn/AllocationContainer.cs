using System;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.ConnectionDlg;
using Microsoft.SqlServer.Management.UI.VSIntegration;

namespace InternalsViewer.SSMSAddIn
{
    /// <summary>
    /// 
    /// </summary>
    public partial class AllocationContainer : UserControl
    {
        private WindowManager windowManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="AllocationContainer"/> class.
        /// </summary>
        public AllocationContainer()
        {
            InitializeComponent();

            this.allocationWindowControl.Connect += new EventHandler(AllocationWindow_Connect);
            this.allocationWindowControl.ViewPage += new EventHandler<PageEventArgs>(AllocationWindowControl_ViewPage);
        }

        /// <summary>
        /// Handles the ViewPage event of the AllocationWindowControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="InternalsViewer.Internals.Pages.PageEventArgs"/> instance containing the event data.</param>
        void AllocationWindowControl_ViewPage(object sender, PageEventArgs e)
        {
            windowManager.CreatePageViewerWindow(new RowIdentifier(e.Address, 0));
        }

        /// <summary>
        /// Handles the Connect event of the AllocationWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        void AllocationWindow_Connect(object sender, EventArgs e)
        {
            this.Connect();
        }

        public void Connect()
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

        /// <summary>
        /// Gets or sets the window manager.
        /// </summary>
        /// <value>The window manager.</value>
        internal WindowManager WindowManager
        {
            get { return windowManager; }
            set { windowManager = value; }
        }
    }
}
