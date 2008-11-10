using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace InternalsViewer.UI
{
    public partial class DecodeWindow : UserControl
    {
        private PageViewerWindow parentWindow;

        public DecodeWindow(PageViewerWindow parentWindow)
        {
            InitializeComponent();
            
            this.parentWindow = parentWindow;
        }
    }
}
