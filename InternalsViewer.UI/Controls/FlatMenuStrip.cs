using System.Windows.Forms;

namespace InternalsViewer.UI.Controls;

#pragma warning disable CA1416

/// <summary>
/// Menu Strip without the rounded corners
/// </summary>
internal class FlatMenuStrip : ToolStrip
{
    private ToolStripProfessionalRenderer renderer;

    public FlatMenuStrip()
    {
        Dock = DockStyle.Top;
        GripStyle = ToolStripGripStyle.Hidden;

        AutoSize = false;

        SetRenderer();
    }

    /// <summary>
    /// Sets the renderer.
    /// </summary>
    private void SetRenderer()
    {
        if ((Renderer is ToolStripProfessionalRenderer) && (Renderer != renderer))
        {
            if (renderer == null)
            {
                renderer = new ToolStripProfessionalRenderer();

                renderer.RoundedEdges = false;
            }

            Renderer = renderer;
        }
    }
}