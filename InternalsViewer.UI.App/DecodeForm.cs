using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InternalsViewer.UI.App;

public partial class DecodeForm : Form
{
    public DecodeForm()
    {
        InitializeComponent();
    }

    public void Show(PageViewerWindow pageViewerWindow)
    {
        decodeWindow.ParentWindow = pageViewerWindow;

        Show();
    }
}
