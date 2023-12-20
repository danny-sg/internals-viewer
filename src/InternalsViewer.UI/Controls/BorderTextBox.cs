using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls;

/// <summary>
/// Taken from https://stackoverflow.com/a/39420512
/// </summary>
public class BorderTextBox : TextBox
{
    const int WM_NCPAINT = 0x85;
    const uint RDW_INVALIDATE = 0x1;
    const uint RDW_IUPDATENOW = 0x100;
    const uint RDW_FRAME = 0x400;
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    
    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprc, IntPtr hrgn, uint flags);

    private Color borderColor = Color.Blue;

    public Color BorderColor
    {
        get { return borderColor; }
        set
        {
            borderColor = value;

            RedrawWindow(Handle, 
                         IntPtr.Zero, 
                         IntPtr.Zero,
                         RDW_FRAME | RDW_IUPDATENOW | RDW_INVALIDATE);
        }
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_NCPAINT 
            && BorderColor != Color.Transparent 
            && BorderStyle == BorderStyle.Fixed3D)
        {
            var hdc = GetWindowDC(Handle);

            using (var g = Graphics.FromHdcInternal(hdc))
            {
                using (var p = new Pen(BorderColor))
                {
                    g.DrawRectangle(p, new Rectangle(0, 0, Width - 1, Height - 1));
                }
            }

            ReleaseDC(Handle, hdc);
        }
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
     
        RedrawWindow(Handle, 
                     IntPtr.Zero, 
                     IntPtr.Zero,
                     RDW_FRAME | RDW_IUPDATENOW | RDW_INVALIDATE);
    }
}
