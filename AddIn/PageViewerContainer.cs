using System;
using System.Windows.Forms;
using EnvDTE;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.SSMSAddIn
{
    public partial class PageViewerContainer : UserControl
    {
        private Window window;
        private WindowManager windowManager;

        internal WindowManager WindowManager
        {
            get { return windowManager; }
            set { windowManager = value; }
        }

        public PageViewerContainer()
        {
            InitializeComponent();

            this.pageViewerWindow.PageChanged += new EventHandler<PageEventArgs>(PageViewerWindow_PageChanged);
            this.pageViewerWindow.OpenDecodeWindow += new EventHandler(PageViewerWindow_OpenDecodeWindow);
        }

        void PageViewerWindow_OpenDecodeWindow(object sender, EventArgs e)
        {
            this.WindowManager.CreateDecodeWindow().DecodeWindow.ParentWindow = this.PageViewerWindow;
        }

        private void PageViewerWindow_PageChanged(object sender, PageEventArgs e)
        {
            this.Window.Caption = "Page Viewer " + e.Address.ToString();
        }

        public Window Window
        {
            get { return this.window; }
            set { this.window = value; }
        }

        public InternalsViewer.UI.PageViewerWindow PageViewerWindow
        {
            get { return this.pageViewerWindow; }
        }
    }
}
