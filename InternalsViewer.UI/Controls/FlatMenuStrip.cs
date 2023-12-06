﻿using System;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls;

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

        // this.SetRenderer();
    }

    /// <summary>
    /// Raises the <see cref="E:System.Windows.Forms.ToolStrip.RendererChanged"/> event.
    /// </summary>
    /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
    protected override void OnRendererChanged(EventArgs e)
    {
        base.OnRendererChanged(e);

        // this.SetRenderer();
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