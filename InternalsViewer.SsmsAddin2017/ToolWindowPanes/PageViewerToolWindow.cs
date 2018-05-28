using System.Runtime.InteropServices;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI;
using Microsoft.VisualStudio.Shell;

namespace InternalsViewer.SsmsAddin2017.ToolWindowPanes
{
    [Guid("c04e00b9-7b3b-4249-beca-fe31977299e1")]
    public class PageViewerToolWindow : ToolWindowPane
    {
        public Control Control { get; set; }

        public PageViewerWindow PageViewerWindow { get; set; }

        public override IWin32Window Window => Control;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageViewerToolWindow"/> class.
        /// </summary>
        public PageViewerToolWindow() : base(null)
        {
            Caption = "PageViewerToolWindow";

            PageViewerWindow = new PageViewerWindow();
            
            Control = PageViewerWindow;
        }

        public void LoadPage(string connectionString, PageAddress pageAddress)
        {
            Caption = $"Page {pageAddress.ToString()}";

            PageViewerWindow.LoadPage(connectionString, pageAddress);
        }
    }
}
