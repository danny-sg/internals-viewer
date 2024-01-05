using System;
using System.Drawing;
using InternalsViewer.UI.App.vNext.Helpers;
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

        BackgroundPaint = GetExtentPaint(SystemColors.Control, SystemColors.ControlLightLight);
        ExtentPaint = GetExtentPaint(Color.Black, Color.Black);
        BorderPaint = GetBorderPaint();
    }

    public bool IsDrawBorder { get; set; }

    public SKPaint BackgroundPaint { get; set; }

    public Color BackgroundColour { get; set; }

    public Color UnusedColour { get; set; }

    public Color BorderColour { get; set; }

    public Size ExtentSize { get; }

    public SKPaint ExtentPaint { get; set; }

    public SKPaint BorderPaint { get; init; }

    public void SetExtentColour(Color colourFrom, Color colourTo)
    {
        var rect = new SKRect(0, 0, ExtentSize.Width, ExtentSize.Height);

        var shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Right, rect.Top),
            new[] { colourFrom.ToSkColor(), colourTo.ToSkColor() },
            null,
            SKShaderTileMode.Repeat);

        ExtentPaint.Shader = shader;
    }

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


    private SKPaint GetExtentPaint(Color colourFrom, Color colourTo)
    {
        var rect = new SKRect(0, 0, ExtentSize.Width, ExtentSize.Height);
        
        var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(rect.Left, rect.Top),
                                                   new SKPoint(rect.Right, rect.Top),
                                                   new[] { colourFrom.ToSkColor(), colourTo.ToSkColor() },
                                                   null,
                                                   SKShaderTileMode.Repeat)
        };

        return paint;
    }

    internal void DrawExtent(SKCanvas g, SKRect rect)
    {
        if (IsDrawBorder)
        {
            g.DrawRect(rect, BorderPaint);
        }

        g.DrawRect(rect, ExtentPaint);
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

            g.DrawRect(extentRectangle, BackgroundPaint);
        }
    }

    internal void DrawPageLines(SKCanvas g,
                                int extentsHorizontal,
                                int extentsVertical,
                                int extentsRemaining)
    {
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