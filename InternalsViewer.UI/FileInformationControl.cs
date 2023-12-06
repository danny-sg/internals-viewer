using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.UI;

public partial class FileInformationControl : UserControl
{
    public const int DefaultHeight = 60;

    private readonly Color spaceColour = Color.Salmon;
    private readonly float spaceUsed;

    public FileInformationControl(DatabaseFile fileInfo)
    {
        InitializeComponent();

        Height = DefaultHeight;

        base.Dock = DockStyle.Bottom;
        databaseFileBindingSource.DataSource = fileInfo;
        spaceUsed = ((100F / fileInfo.TotalExtents) * fileInfo.UsedExtents / 100);
    }

    private void SpaceUsedPanel_Paint(object sender, PaintEventArgs e)
    {
        var pfsSpaceBrush = new LinearGradientBrush(spaceUsedPanel.ClientRectangle,
            ExtentColour.LightBackgroundColour(spaceColour),
            spaceColour,
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(pfsSpaceBrush,
            new Rectangle(spaceUsedPanel.ClientRectangle.Location,
                new Size((int)(spaceUsedPanel.Width * spaceUsed),
                    spaceUsedPanel.Height)));

        ControlPaint.DrawBorder(e.Graphics, spaceUsedPanel.ClientRectangle, Color.DarkGray, ButtonBorderStyle.Solid);
    }
}