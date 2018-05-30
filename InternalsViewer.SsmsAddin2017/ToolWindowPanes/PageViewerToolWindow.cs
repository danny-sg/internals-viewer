using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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
            Caption = "Page";

            PageViewerWindow = new PageViewerWindow();

            PageViewerWindow.OpenDecodeWindow += PageViewerWindow_OpenDecodeWindow;

            Control = PageViewerWindow;
        }

        private void PageViewerWindow_OpenDecodeWindow(object sender, System.EventArgs e)
        {
            var package = Package as Package;

            var window = package.FindToolWindow(typeof(DecodeToolWindow), 0, true);
            
            if (window?.Frame == null)
            {
                throw new NotSupportedException("Cannot create Internals Viewer");
            }
            
            ((DecodeToolWindow)window).DecodeWindow.ParentWindow = PageViewerWindow;

            var windowFrame = (IVsWindowFrame)window.Frame;

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        public void LoadPage(string connectionString, PageAddress pageAddress)
        {
            Caption = $"Page {pageAddress.ToString()}";

            PageViewerWindow.LoadPage(connectionString, pageAddress);
        }
    }
}
