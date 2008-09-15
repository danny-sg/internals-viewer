using System;
using System.Windows.Forms;

namespace InternalsViewer.UI
{
    /// <summary>
    /// Menu Strip without the rounded corners
    /// </summary>
    internal class FlatMenuStrip : ToolStrip
    {
        private ToolStripProfessionalRenderer renderer;

        public FlatMenuStrip()
        {
            this.Dock = DockStyle.Top;
            GripStyle = ToolStripGripStyle.Hidden;

            this.AutoSize = false;

            this.SetRenderer();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.ToolStrip.RendererChanged"/> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
        protected override void OnRendererChanged(EventArgs e)
        {
            base.OnRendererChanged(e);

            this.SetRenderer();
        }

        /// <summary>
        /// Sets the renderer.
        /// </summary>
        private void SetRenderer()
        {
            if ((Renderer is ToolStripProfessionalRenderer) && (Renderer != this.renderer))
            {
                if (this.renderer == null)
                {
                    this.renderer = new ToolStripProfessionalRenderer();

                    this.renderer.RoundedEdges = false;
                }

                Renderer = this.renderer;
            }
        }
    }
}
