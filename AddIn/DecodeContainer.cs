using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.UI;

namespace InternalsViewer.SSMSAddIn
{
    public partial class DecodeContainer : UserControl
    {
        public DecodeContainer()
        {
            InitializeComponent();
        }

        public DecodeWindow DecodeWindow
        {
            get { return this.decodeWindow; }
        }
    }
}
