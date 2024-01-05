using System;
using System.Drawing;
using SkiaSharp;

namespace InternalsViewer.UI.App.vNext.Controls.Renderers;

public class ExtentRenderer : IDisposable
{
    public ExtentRenderer(Color backgroundColour, Color unusedColour, Color borderColour, Size extentSize)
    {
        BackgroundColour = backgroundColour;
        UnusedColour = unusedColour;
        BorderColour = borderColour;
        ExtentSize = extentSize;

        ExtentPaint = GetExtentPaint();
        BorderPaint = GetBorderPaint();
    }

    public bool IsDrawBorder { get; set; }

    public Color BackgroundColour { get; set; }

    public Color UnusedColour { get; set; }

    public Color BorderColour { get; set; }

    public Size ExtentSize { get; }

    public SKPaint ExtentPaint { get; init; }

    public SKPaint BorderPaint { get; init; }

    private SKPaint GetBorderPaint()
    {
        var paint = new SKPaint
        {
            Color = new SKColor(BorderColour.R, BorderColour.G, BorderColour.B),
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        return paint;
    }


    private SKPaint GetExtentPaint()
    {
        var rect = new SKRect(0, 0, ExtentSize.Width, ExtentSize.Height);

        var c = SystemColors.Control;
        var c2 = SystemColors.ControlLightLight;

        var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Top),
                new[] { new SKColor(c.R, c.G, c.B), new SKColor(c2.R, c2.G, c2.B) },
                null,
                SKShaderTileMode.Repeat)
        };

        return paint;
    }

    internal void DrawExtent(SKCanvas g, SKRect rect)
    {
        g.DrawRect(rect, ExtentPaint);

        if (IsDrawBorder)
        {
            g.DrawRect(rect, BorderPaint);
        }
    }

    internal void DrawBackgroundExtents(SKCanvas g,
        int extentsHorizontal,
        int extentsVertical,
        int extentsRemaining)
    {
        // Extents are drawn as columns
        for (var extentColumn = 0; extentColumn < extentsHorizontal; extentColumn++)
        {
            // Column is either full height or one row less if a remaining extent column
            var extentHeight = extentColumn < extentsRemaining
                ? (extentsVertical + 1) * ExtentSize.Height
                : extentsVertical * ExtentSize.Height;


            var extentRectangle = new SKRect(extentColumn * ExtentSize.Width,
                0,
                ExtentSize.Width,
                extentHeight);

            g.DrawRect(extentRectangle, ExtentPaint);
        }

        if (IsDrawBorder)
        {
            var pageWidth = ExtentSize.Width / 8F;

            for (var page = 0; page <= (extentsHorizontal - 1) * 8; page++)
            {
                var extent = Math.Ceiling(page / 8F);

                var linePosition = page * pageWidth;
                var lineHeight = extent < extentsRemaining
                    ? (extentsVertical + 1) * ExtentSize.Height
                    : extentsVertical * ExtentSize.Height;

                // Draw vertical lines to separate the pages in the columns
                g.DrawLine(linePosition,
                    0,
                    linePosition,
                    lineHeight,
                    BorderPaint);
            }
            for (var k = 0; k <= extentsVertical + 1; k++)
            {
                var width = k == extentsVertical + 1
                    ? ExtentSize.Width * (extentsRemaining - 1)
                    : ExtentSize.Width * (extentsHorizontal - 1);

                // Draw horizontal lines to separate the extents
                g.DrawLine(new SKPoint(0, k * ExtentSize.Height),
                           new SKPoint(width, k * ExtentSize.Height),
                           BorderPaint);
            }
        }

    }

    public void Dispose()
    {
        ExtentPaint.Dispose();
        BorderPaint.Dispose();
    }
}