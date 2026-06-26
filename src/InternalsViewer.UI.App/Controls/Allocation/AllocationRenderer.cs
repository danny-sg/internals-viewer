using System;
using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed class AllocationRenderer : IDisposable
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

    private Color LastColourFrom { get; set; } = Color.White;

    private Color LastColourTo { get; set; } = Color.White;

    public void SetAllocationColour(Color colourFrom, Color colourTo)
    {
        if (colourFrom == LastColourFrom && colourTo == LastColourTo)
        {
            return;
        }

        LastColourFrom = colourFrom;
        LastColourTo = colourTo;

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
                                                   [colourFrom.ToSkColor(), colourTo.ToSkColor()],
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
                                                   [colourFrom.ToSkColor(), colourTo.ToSkColor()],
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
        var normalHeight = extentsVertical * ExtentSize.Height;
        var totalWidth = extentsHorizontal * ExtentSize.Width;

        if (extentsRemaining > 0)
        {
            // Columns with a partial extra row are taller — draw them as one rect
            var tallWidth = extentsRemaining * ExtentSize.Width;
            var tallHeight = (extentsVertical + 1) * ExtentSize.Height;

            g.DrawRect(new SKRect(0, 0, tallWidth, tallHeight), BackgroundPaint);

            // Remaining shorter columns as a second rect
            if (extentsRemaining < extentsHorizontal)
            {
                g.DrawRect(new SKRect(tallWidth, 0, totalWidth, normalHeight), BackgroundPaint);
            }
        }
        else
        {
            g.DrawRect(new SKRect(0, 0, totalWidth, normalHeight), BackgroundPaint);
        }
    }

    internal void DrawPageLines(SKCanvas g,
        int extentsHorizontal,
        int extentsVertical,
        int extentsRemaining)
    {
        if (!IsDrawBorder)
        {
            return;
        }

        var pageWidth = ExtentSize.Width / 8F;
        var normalHeight = extentsVertical * ExtentSize.Height;
        var tallHeight = (extentsVertical + 1) * ExtentSize.Height;
        var fullWidth = ExtentSize.Width * extentsHorizontal;

        // Precompute the boundary page index — the closing border of the last tall column
        // must also be drawn tall, so use <= not <
        var tallBoundaryPage = extentsRemaining * 8;

        for (var page = 0; page <= extentsHorizontal * 8; page++)
        {
            var lineHeight = extentsRemaining > 0 && page <= tallBoundaryPage
                ? tallHeight
                : normalHeight;

            var x = page * pageWidth;

            g.DrawLine(x, 0, x, lineHeight, BorderPaint);
        }

        // Full-width horizontal row separators (top border through bottom of last full row)
        for (var k = 0; k <= extentsVertical; k++)
        {
            var y = k * ExtentSize.Height;

            g.DrawLine(new SKPoint(0, y), new SKPoint(fullWidth, y), BorderPaint);
        }

        // Bottom border of partial last row — only drawn when one exists
        if (extentsRemaining > 0)
        {
            var y = (extentsVertical + 1) * ExtentSize.Height;
            var remainingWidth = extentsRemaining * ExtentSize.Width; // was extentsRemaining - 1

            g.DrawLine(new SKPoint(0, y), new SKPoint(remainingWidth, y), BorderPaint);
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