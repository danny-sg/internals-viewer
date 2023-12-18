using System.Drawing;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls;

#pragma warning disable CA1416

internal class BorderPanel : Panel
{
    public BorderPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
    }
}