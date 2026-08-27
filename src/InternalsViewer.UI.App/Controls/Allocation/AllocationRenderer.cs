using System;
using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Allocation;

public sealed class AllocationRenderer : IDisposable
{
    /// <summary>
    /// How far the wash over a selected page lifts what is under it, leaving the allocation colour readable through it
    /// </summary>
    private const byte SelectedPageAlpha = 128;

    public AllocationRenderer(Color borderColour,
                              Size extentSize)
    {
        BorderColour = borderColour;

        ExtentSize = extentSize;

        BackgroundPaint = GetExtentPaint(SystemColors.Control, SystemColors.ControlLightLight);
        AllocationPaint = GetExtentPaint(Color.Black, Color.Black);
        PagePaint = GetPagePaint(Color.Black, Color.Black);

        PageMarkerPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };

        BorderPaint = GetBorderPaint();

        SelectedPagePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(SelectedPageAlpha),
            Style = SKPaintStyle.Fill
        };

        SelectedRowPaint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Fill
        };
    }

    public bool IsDrawBorder { get; set; }

    private SKPaint BackgroundPaint { get; }

    private Color BorderColour { get; }

    private Size ExtentSize { get; }

    private SKPaint AllocationPaint { get; }

    private SKPaint PagePaint { get; }

    private SKPaint PageMarkerPaint { get; }

    private SKPaint BorderPaint { get; }

    private SKPaint SelectedPagePaint { get; }

    private SKPaint SelectedRowPaint { get; }

    private Color LastColourFrom { get; set; } = Color.White;

    private Color LastColourTo { get; set; } = Color.White;

    private SKPathBuilder PathBuilder { get; } = new();

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

        AllocationPaint.Shader?.Dispose();
        AllocationPaint.Shader = extentShader;

        var pageRect = new SKRect(0, 0, ExtentSize.Width / 8F, ExtentSize.Height);

        var pageShader = SKShader.CreateLinearGradient(new SKPoint(pageRect.Left, pageRect.Top),
                                                       new SKPoint(pageRect.Right, pageRect.Top),
                                                       colours,
                                                       null,
                                                       SKShaderTileMode.Repeat);

        PagePaint.Shader?.Dispose();
        PagePaint.Shader = pageShader;
    }

    public void Dispose()
    {
        AllocationPaint.Shader?.Dispose();
        PagePaint.Shader?.Dispose();
        BackgroundPaint.Shader?.Dispose();

        AllocationPaint.Dispose();
        BorderPaint.Dispose();
        BackgroundPaint.Dispose();
        PagePaint.Dispose();
        PageMarkerPaint.Dispose();
        SelectedPagePaint.Dispose();
        SelectedRowPaint.Dispose();

        PathBuilder.Dispose();
    }

    internal void DrawExtent(SKCanvas g, SKRect rect)
    {
        g.DrawRect(rect, AllocationPaint);
    }

    internal void DrawPage(SKCanvas g, SKRect rect, LayerType layerLayerType)
    {
        switch (layerLayerType)
        {
            case LayerType.Fill:
                g.DrawRect(rect, PagePaint);
                break;
        }
    }

    internal void DrawSelectedRow(SKCanvas g, SKRect pageRect, int slot, int slotCount)
    {
        if (slotCount <= 0)
        {
            return;
        }

        g.DrawRect(pageRect, SelectedPagePaint);

        var rowHeight = Math.Max(2F, pageRect.Height / slotCount);

        var top = pageRect.Top + Math.Min(slot, slotCount - 1) * pageRect.Height / slotCount;

        g.DrawRect(new SKRect(pageRect.Left, top, pageRect.Right, top + rowHeight), SelectedRowPaint);
    }

    internal void DrawPageMarker(SKCanvas g, SKRect rect, AllocationLayer layer, SKColor colour)
    {
        if (layer.LayerType == LayerType.Fill)
        {
            return;
        }

        if (PageMarkerPaint.Color != colour)
        {
            PageMarkerPaint.Color = colour;
        }

        switch (layer.LayerType)
        {
            case LayerType.TopLeft:
                {
                    var insetX = rect.Width * 0.12f;
                    var insetY = rect.Height * 0.12f;

                    var originX = rect.Left + insetX - (rect.Left == 0 ? 0 : 1);
                    var originY = rect.Top + insetY - (rect.Top == 0 ? 0 : 1);

                    var wedgeWidth = rect.Width * 0.45f;
                    var wedgeHeight = rect.Height * 0.45f;

                    var p1 = new SKPoint(originX, originY);
                    var p2 = new SKPoint(originX + wedgeWidth, originY);
                    var p3 = new SKPoint(originX, originY + wedgeHeight);

                    PathBuilder.MoveTo(p1);
                    PathBuilder.LineTo(p2);
                    PathBuilder.LineTo(p3);

                    using var path = PathBuilder.Detach();

                    g.DrawPath(path, PageMarkerPaint);
                }

                break;
        }
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
}