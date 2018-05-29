using System.Windows.Forms;
using InternalsViewer.UI;

namespace InternalsViewer.SsmsAddin2017.ToolWindowPanes
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

    [Guid("7cd5ed8f-7533-4577-9a00-afc0a79a1182")]
    public class DecodeToolWindow : ToolWindowPane
    {
        public Control Control { get; set; }

        public DecodeWindow DecodeWindow { get; set; }

        public override IWin32Window Window => Control;

        public DecodeToolWindow() : base(null)
        {
            Caption = "Encode and Find";

            DecodeWindow = new DecodeWindow();
            
            Control = DecodeWindow;
        }
    }
}
