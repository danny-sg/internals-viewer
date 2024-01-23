using System;
using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Allocation;

public class AllocationRenderer : IDisposable
{
    public AllocationRenderer(Color borderColour,
                              Size extentSize)
    {
        BorderColour = borderColour;

        ExtentSize = extentSize;

        BackgroundPaint = GetExtentPaint(SystemColors.Control, SystemColors.ControlLightLight);
        AllocationPaint = GetExtentPaint(Color.Black, Color.Black);
        PagePaint = GetPagePaint(Color.Black, Color.Black);
        BorderPaint = GetBorderPaint();
    }

    public bool IsDrawBorder { get; set; }

    private SKPaint BackgroundPaint { get; }

    private Color BorderColour { get; }

    private Size ExtentSize { get; }

    private SKPaint AllocationPaint { get; }

    private SKPaint PagePaint { get; }

    private SKPaint BorderPaint { get; }

    public void SetAllocationColour(Color colourFrom, Color colourTo)
    {
        var extentRect = new SKRect(0, 0, ExtentSize.Width, ExtentSize.Height);

        var colours = new[]
                      {
                          colourFrom.ToSkColor(),
                          colourTo.ToSkColor()
                      };

        var extentShader = SKShader.CreateLinearGradient(new SKPoint(extentRect.Left, extentRect.Top),
                                                         new SKPoint(extentRect.Right, extentRect.Top),
                                                         colours,
                                                         null,
                                                         SKShaderTileMode.Repeat);

        AllocationPaint.Shader = extentShader;

        var pageRect = new SKRect(0, 0, ExtentSize.Width / 8F, ExtentSize.Height);

        var pageShader = SKShader.CreateLinearGradient(new SKPoint(pageRect.Left, pageRect.Top),
                                                       new SKPoint(pageRect.Right, pageRect.Top),
                                                       colours,
                                                       null,
                                                       SKShaderTileMode.Repeat);

        PagePaint.Shader = pageShader;
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

    private SKPaint GetPagePaint(Color colourFrom, Color colourTo)
    {
        var rect = new SKRect(0, 0, ExtentSize.Width / 8F, ExtentSize.Height);

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
        g.DrawRect(rect, AllocationPaint);
    }

    internal void DrawPage(SKCanvas g, SKRect rect)
    {
        g.DrawRect(rect, PagePaint);
    }

    internal void DrawBackgroundExtents(SKCanvas g,
                                        int extentsHorizontal,
                                        int extentsVertical,
                                        int extentsRemaining)
    {
        // Extents are drawn as columns
        for (var extentColumn = 0; extentColumn <= extentsHorizontal; extentColumn++)
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

            for (var page = 0; page <= extentsHorizontal * 8; page++)
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
                    : ExtentSize.Width * extentsHorizontal;

                // Draw horizontal lines to separate the extents
                g.DrawLine(new SKPoint(0, k * ExtentSize.Height),
                           new SKPoint(width, k * ExtentSize.Height),
                           BorderPaint);
            }
        }
    }

    public void Dispose()
    {
        AllocationPaint.Dispose();
        BorderPaint.Dispose();
        BackgroundPaint.Dispose();
        PagePaint.Dispose();
    }
}