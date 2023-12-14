using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using InternalsViewer.UI.Allocations;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Renderers;

/// <summary>
/// Renders extents and pages
/// </summary>
internal sealed class PageExtentRenderer : IDisposable
{
    public Color BackgroundColour { get; }

    public Color UnusedColour { get; }
    
    public Pen? BorderPen { get; private set; }
    
    public LinearGradientBrush? ExtentPageBrush { get; private set; }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        ExtentPageBrush?.Dispose();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageExtentRenderer"/> class.
    /// </summary>
    /// <param name="backgroundColour">The background colour.</param>
    /// <param name="unusedColour">The unused colour.</param>
    internal PageExtentRenderer(Color backgroundColour, Color unusedColour)
    {
        BackgroundColour = backgroundColour;
        UnusedColour = unusedColour;
    }

    /// <summary>
    /// Draws the LSN map page.
    /// </summary>
    /// <param name="g">The graphics object.</param>
    /// <param name="rect">The page Rectangle.</param>
    /// <param name="lsn">The LSN.</param>
    /// <param name="maxLsn">The max LSN.</param>
    /// <param name="minLsn">The min LSN.</param>
    internal static void DrawLsnMapPage(Graphics g, Rectangle rect, decimal lsn, decimal maxLsn, decimal minLsn)
    {
        var hueValue = 140;

        var step = 230F / ((float)(maxLsn - minLsn));

        var value = (int)(step / 1.175F * (float)lsn);

        var pageColour = HsvColour.HsvToColor(hueValue, (255 - value) % 255, value);

        g.FillRectangle(new SolidBrush(pageColour), rect);
        g.DrawRectangle(Pens.White, rect);
    }

    /// <summary>
    /// Creates the brushes and pens used for the renderer
    /// </summary>
    /// <param name="extentSize">Size of the extent.</param>
    internal void CreateBrushesAndPens(Size extentSize)
    {
        var gradientRect = new Rectangle(0, 0, extentSize.Width, extentSize.Height);

        ExtentPageBrush = new LinearGradientBrush(gradientRect,
                                                  BackgroundColour,
                                                  UnusedColour,
                                                  LinearGradientMode.Horizontal);

        BorderPen = new Pen(PageBorderColour);
    }

    /// <summary>
    /// Draws an extent at a specified Rectangle size and location
    /// </summary>
    internal void DrawExtent(Graphics g, Rectangle rect)
    {
        if(ExtentPageBrush == null || BorderPen== null)
        {
            return;
        }

        g.FillRectangle(ExtentPageBrush, rect);

        if (DrawBorder)
        {
            g.DrawRectangle(BorderPen, rect);

            for (var j = 1; j < 9; j++)
            {
                g.DrawLine(BorderPen,
                           rect.X + (j * rect.Width / 8),
                           rect.Y,
                           (rect.X + j * rect.Width / 8),
                           rect.Y + rect.Height);
            }
        }
    }

    /// <summary>
    /// Draws an individual page.
    /// </summary>
    /// <param name="g">The graphics object.</param>
    /// <param name="rect">The page Rectangle.</param>
    /// <param name="layerType">Type of the layer.</param>
    internal void DrawPage(Graphics g, Rectangle rect, AllocationLayerType layerType)
    {
        if (ExtentPageBrush == null || BorderPen == null)
        {
            return;
        }

        switch (layerType)
        {
            case AllocationLayerType.Standard:

                g.FillRectangle(ExtentPageBrush, rect);
                break;

            case AllocationLayerType.TopLeftCorner:

                var path = new GraphicsPath();

                path.AddLine(rect.X, rect.Y, rect.X + rect.Width / 1.6F, rect.Y);
                path.AddLine(rect.X + (rect.Width / 1.6F), rect.Y, rect.X, rect.Y + (rect.Height / 1.6F));
                path.AddLine(rect.X, rect.Y + (rect.Height / 1.6F), rect.X, rect.Y);
                path.CloseFigure();

                g.SmoothingMode = SmoothingMode.AntiAlias;

                g.FillPath(ExtentPageBrush, path);
                break;
        }

        if (DrawBorder)
        {
            g.DrawRectangle(BorderPen, rect);
        }
    }

    /// <summary>
    /// Draws the background extents.
    /// </summary>
    /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
    /// <param name="extentSize">Size of the extent.</param>
    /// <param name="extentsHorizontal">The extents horizontal.</param>
    /// <param name="extentsVertical">The extents vertical.</param>
    /// <param name="extentsRemaining">The extents remaining.</param>
    internal void DrawBackgroundExtents(PaintEventArgs e,
                                        Size extentSize,
                                        int extentsHorizontal,
                                        int extentsVertical,
                                        int extentsRemaining)
    {
        ResizeExtentBrush(extentSize);

        SetExtentBrushColour(BackgroundColour, UnusedColour);

        for (var i = 0; i < extentsHorizontal; i++)
        {
            Rectangle rect;

            if (i < extentsRemaining)
            {
                rect = new Rectangle(i * extentSize.Width,
                    0,
                    extentSize.Width,
                    (extentsVertical + 1) * extentSize.Height);
            }
            else
            {
                rect = new Rectangle(i * extentSize.Width,
                    0,
                    extentSize.Width,
                    extentsVertical * extentSize.Height);
            }

            e.Graphics.FillRectangle(ExtentPageBrush!, rect);

            for (var j = 0; j <= 8; j++)
            {
                if (i < extentsRemaining)
                {
                    e.Graphics.DrawLine(BorderPen!,
                        j * (extentSize.Width / 8) + (extentSize.Width * i),
                        0,
                        j * (extentSize.Width / 8) + (extentSize.Width * i),
                        (extentsVertical + 1) * extentSize.Height);
                }
                else
                {
                    e.Graphics.DrawLine(BorderPen!,
                        j * (extentSize.Width / 8) + (extentSize.Width * i),
                        0,
                        j * (extentSize.Width / 8) + (extentSize.Width * i),
                        extentsVertical * extentSize.Height);
                }
            }

            for (var k = 0; k < extentsVertical + 1; k++)
            {
                e.Graphics.DrawLine(BorderPen!,
                    0,
                    k * extentSize.Height,
                    extentSize.Width * extentsHorizontal,
                    k * extentSize.Height);
            }
        }
    }

    /// <summary>
    /// Draws the selection.
    /// </summary>
    /// <param name="g">The graphics object.</param>
    /// <param name="rect">The page Rectangle.</param>
    internal void DrawSelection(Graphics g, Rectangle rect)
    {
        g.FillRectangle(Brushes.LightGray, rect);

        if (DrawBorder)
        {
            g.DrawRectangle(BorderPen!, rect);

            for (var j = 1; j < 9; j++)
            {
                g.DrawLine(BorderPen!,
                    rect.X + (j * rect.Width / 8),
                    rect.Y,
                    (rect.X + j * rect.Width / 8),
                    rect.Y + rect.Height);
            }
        }
    }

    /// <summary>
    /// Sets the extent brush colour.
    /// </summary>
    internal void SetExtentBrushColour(Color foreColour, Color backColour)
    {
        if (ExtentPageBrush!.LinearColors[0] != foreColour || ExtentPageBrush.LinearColors[1] != backColour)
        {
            ExtentPageBrush.LinearColors = new Color[2] { foreColour, backColour };
        }
    }

    /// <summary>
    /// Resizes the extent brush.
    /// </summary>
    /// <param name="extentSize">Size of the extent.</param>
    internal void ResizeExtentBrush(Size extentSize)
    {
        ExtentPageBrush!.ResetTransform();
        ExtentPageBrush!.ScaleTransform(extentSize.Height / ExtentPageBrush.Rectangle.Height,
            extentSize.Width / ExtentPageBrush.Rectangle.Width,
            MatrixOrder.Append);
    }

    /// <summary>
    /// Resizes the page brush.
    /// </summary>
    /// <param name="pageSize">Size of the page.</param>
    internal void ResizePageBrush(Size pageSize)
    {
        ExtentPageBrush!.ResetTransform();
        ExtentPageBrush!.ScaleTransform((pageSize.Width / ExtentPageBrush.Rectangle.Width) / 8,
            (pageSize.Width / ExtentPageBrush.Rectangle.Width) / 8,
            MatrixOrder.Append);
    }

    /// <summary>
    /// Gets or sets the page border colour.
    /// </summary>
    /// <value>The page border colour.</value>
    public Color PageBorderColour { get; set; } = Color.White;

    /// <summary>
    /// Gets or sets a value indicating whether to draw borders around the extents and pages.
    /// </summary>
    /// <value><c>true</c> if [draw border]; otherwise, <c>false</c>.</value>
    public bool DrawBorder { get; set; } = true;
}