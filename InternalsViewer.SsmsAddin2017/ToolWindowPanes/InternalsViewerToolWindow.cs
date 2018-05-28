using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.UI;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.SqlServer.Management.UI.ConnectionDlg;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace InternalsViewer.SsmsAddin2017.ToolWindowPanes
{
    [Guid("6d5ec8dc-9cc8-4ca7-98eb-d82d00506912")]
    public class InternalsViewerToolWindow : ToolWindowPane
    {
        public Control Control { get; set; }

        public AllocationWindow AllocationWindow { get; set; }

        public override IWin32Window Window => Control;

        public int PageViewerCount { get; set; } = 1;

        public InternalsViewerToolWindow() : base(null)
        {
            Caption = "Internals Viewer";
            
            AllocationWindow = new AllocationWindow();
            Control = AllocationWindow;

            AllocationWindow.Connect += AllocationWindow_Connect;
            AllocationWindow.ViewPage += AllocationWindow_ViewPage;
        }

        private void AllocationWindow_ViewPage(object sender, Internals.Pages.PageEventArgs e)
        {
            var package = Package as Package;

            var window = package.FindToolWindow(typeof(PageViewerToolWindow), PageViewerCount, true);

            PageViewerCount++;

            if (window?.Frame == null)
            {
                throw new NotSupportedException("Cannot create Internals Viewer");
            }

            var connectionString = InternalsViewerConnection.CurrentConnection().ConnectionString;

            ((PageViewerToolWindow)window).LoadPage(connectionString, e.Address);

            var windowFrame = (IVsWindowFrame)window.Frame;

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        private void AllocationWindow_Connect(object sender, EventArgs e)
        {
            Connect();
        }

        public void Connect()
        {
            using (var dialog = new ShellConnectionDialog())
            {
                IServerType serverType = new SqlServerType();

                dialog.AddServer(serverType);

                var connectionInfo = new UIConnectionInfo();

                if (dialog.ShowDialogCollectValues(ServiceCache.MainShellWindow, ref connectionInfo) == DialogResult.OK)
                {
                    ConnectionManager.ConnectInternalsViewer(connectionInfo);

                    AllocationWindow.RefreshDatabases();
                }
            }
        }
    }
}
