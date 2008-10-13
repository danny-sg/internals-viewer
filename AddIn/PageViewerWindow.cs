using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using EnvDTE;

namespace InternalsViewer.SSMSAddIn
{
    public partial class PageViewerWindow : UserControl
    {
        private Window window;

        public PageViewerWindow()
        {
            InitializeComponent();
            this.pageViewerWindow1.PageChanged += new EventHandler<PageEventArgs>(pageViewerWindow1_PageChanged);
        }

        void pageViewerWindow1_PageChanged(object sender, PageEventArgs e)
        {
            this.Window.Caption = "Page Viewer " + e.Address.ToString();
        }

        public Page Page
        {
            get { return this.pageViewerWindow1.Page; }
            set { this.pageViewerWindow1.Page = value; }
        }

        internal void LoadPage(PageAddress pageAddress)
        {
            this.pageViewerWindow1.LoadPage(pageAddress);
        }

        public Window Window
        {
            get { return this.window; }
            set { this.window = value; }
        }

        internal void LoadPage(string connectionString, PageAddress pageAddress)
        {
            this.pageViewerWindow1.LoadPage(connectionString, pageAddress);
        }
    }
}
