using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Draws the sixty four bits of a packed unit, banded by the value each bit belongs to
/// </summary>
/// <remarks>
/// Bit zero is drawn on the left because values fill a unit from the least significant bit upward, so left to right
/// runs in value order. That is the reverse of how the same bytes read in the hex view, hence the labelled ends.
/// </remarks>
public sealed class BitRulerRenderer
{
    public const int UnitBits = 64;

    private const float ByteHeight = 20f;

    private const int BitsPerByte = 8;

    private const float CellHeight = 22f;

    private const float ValueBoxHeight = 20f;

    private const float ValueBoxGap = 0f;

    private const float EndLabelHeight = 14f;

    public static float Height => ByteHeight + CellHeight + ValueBoxGap + ValueBoxHeight + EndLabelHeight;

    private readonly SKPaint _fill = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    private readonly SKPaint _stroke = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKFont _bitFont = new(SKTypeface.FromFamilyName("Cascadia Mono") ?? SKTypeface.Default, 11f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly SKFont _labelFont = new(SKTypeface.FromFamilyName("Segoe UI Variable Text") ?? SKTypeface.Default, 10f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    /// <summary>
    /// Bit positions and decoded values are monospaced, lining them up with the hex the ruler sits beside
    /// </summary>
    private readonly SKFont _monoFont = new(SKTypeface.FromFamilyName("Cascadia Mono") ?? SKTypeface.Default, 10f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    public SKColor TextColour { get; set; } = new(0x20, 0x20, 0x20);

    public SKColor MutedColour { get; set; } = new(0x70, 0x70, 0x70);

    public SKColor BorderColour { get; set; } = new(0xD0, 0xCE, 0xC6);

    public SKColor PaddingColour { get; set; } = new(0xE8, 0xE7, 0xE2);

    public SKColor ValueBoxColour { get; set; } = new(0xFA, 0xF9, 0xF6);

    public SKColor ByteBoxColour { get; set; } = new(0xF0, 0xF0, 0xF0);

    /// <summary>
    /// Rule down each byte boundary, which the value bands underneath it do not line up with
    /// </summary>
    public SKColor ByteDividerColour { get; set; } = SKColors.White;

    /// <summary>
    /// Two tints alternating along the unit, so where one value ends and the next begins is never in doubt
    /// </summary>
    public SKColor[] BandColours { get; set; } = [new(0xD6, 0xE6, 0xF7), new(0xBB, 0xD6, 0xF0)];

    public int SelectedValueIndex { get; set; } = -1;

    private float _cellWidth;

    /// <summary>
    /// Bit the point falls on, or minus one where it is outside the bit row
    /// </summary>
    public int GetBitAt(float x, float y)
    {
        if (_cellWidth <= 0 || y < ByteHeight || y > ByteHeight + CellHeight)
        {
            return -1;
        }

        var bit = (int)((x - ColumnstoreLayout.Margin) / _cellWidth);

        return bit is >= 0 and < UnitBits ? bit : -1;
    }

    public SKColor SelectionColour { get; set; } = new(0x18, 0x5F, 0xA5);

    public List<(SKRect Bounds, int Index)> Draw(SKCanvas canvas, BitpackUnitDetail unit, float width)
    {
        var regions = new List<(SKRect, int)>(unit.Values.Count);

        var cellWidth = MathF.Max(6f, (width - (ColumnstoreLayout.Margin * 2)) / UnitBits);

        var left = ColumnstoreLayout.Margin;

        var cellTop = ByteHeight;

        _cellWidth = cellWidth;

        DrawBytes(canvas, unit, left, cellWidth);

        DrawPadding(canvas, unit, left, cellWidth, cellTop);

        for (var i = 0; i < unit.Values.Count; i++)
        {
            var value = unit.Values[i];

            var bounds = new SKRect(left + (value.BitOffset * cellWidth),
                                    cellTop,
                                    left + ((value.BitOffset + value.BitLength) * cellWidth),
                                    cellTop + CellHeight);

            _fill.Color = BandColours[i % BandColours.Length];

            canvas.DrawRect(bounds, _fill);

            regions.Add((bounds, value.Index));

            DrawValueBox(canvas, value, bounds);
        }

        DrawBits(canvas, unit, left, cellWidth, cellTop);

        DrawByteDividers(canvas, left, cellWidth, cellTop);

        DrawSelection(canvas, regions);

        DrawEndLabels(canvas, left, cellWidth, width);

        return regions;
    }

    private void DrawPadding(SKCanvas canvas, BitpackUnitDetail unit, float left, float cellWidth, float top)
    {
        if (unit.PaddingBits <= 0)
        {
            return;
        }

        var start = UnitBits - unit.PaddingBits;

        _fill.Color = PaddingColour;

        canvas.DrawRect(new SKRect(left + (start * cellWidth), top, left + (UnitBits * cellWidth), top + CellHeight),
                        _fill);
    }

    /// <summary>
    /// The bit values themselves, dropped once the cells are too narrow to hold a digit
    /// </summary>
    private void DrawBits(SKCanvas canvas, BitpackUnitDetail unit, float left, float cellWidth, float top)
    {
        _stroke.Color = BorderColour;

        canvas.DrawRect(new SKRect(left, top, left + (UnitBits * cellWidth), top + CellHeight), _stroke);

        if (cellWidth < _bitFont.MeasureText("0") + 2f)
        {
            return;
        }

        _text.Color = TextColour;

        var baseline = top + ((CellHeight + _bitFont.Size) / 2) - 1f;

        for (var bit = 0; bit < UnitBits; bit++)
        {
            var isSet = (unit.Bits & (1UL << bit)) != 0;

            var glyph = isSet ? "1" : "0";

            _text.Color = isSet ? TextColour : MutedColour;

            var x = left + (bit * cellWidth) + ((cellWidth - _bitFont.MeasureText(glyph)) / 2);

            canvas.DrawText(glyph, x, baseline, SKTextAlign.Left, _bitFont, _text);
        }
    }

    /// <summary>
    /// Rules the bit row at each byte boundary, so a value band spanning one is plain to see
    /// </summary>
    private void DrawByteDividers(SKCanvas canvas, float left, float cellWidth, float top)
    {
        _stroke.Color = ByteDividerColour;

        for (var bit = BitsPerByte; bit < UnitBits; bit += BitsPerByte)
        {
            var x = left + (bit * cellWidth);

            canvas.DrawLine(x, top, x, top + CellHeight, _stroke);
        }
    }

    /// <summary>
    /// The bytes the unit is made of, one box per byte over the eight bits it holds
    /// </summary>
    /// <remarks>
    /// Byte zero is leftmost, matching how the same eight bytes read in the hex view, so a value straddling a box
    /// edge is a value straddling a byte.
    /// </remarks>
    private void DrawBytes(SKCanvas canvas, BitpackUnitDetail unit, float left, float cellWidth)
    {
        var byteWidth = cellWidth * BitsPerByte;

        for (var i = 0; i < UnitBits / BitsPerByte; i++)
        {
            var bounds = new SKRect(left + (i * byteWidth), 0f, left + ((i + 1) * byteWidth), ByteHeight);

            _fill.Color = ByteBoxColour;

            canvas.DrawRect(bounds, _fill);

            _stroke.Color = BorderColour;

            canvas.DrawRect(bounds, _stroke);

            var label = $"{(byte)(unit.Bits >> (i * BitsPerByte)):X2}";

            var textWidth = _monoFont.MeasureText(label);

            if (textWidth + 2f > bounds.Width)
            {
                continue;
            }

            _text.Color = TextColour;

            canvas.DrawText(label,
                            bounds.MidX - (textWidth / 2),
                            bounds.Bottom - ((ByteHeight - _monoFont.Size) / 2),
                            SKTextAlign.Left,
                            _monoFont,
                            _text);
        }
    }

    /// <summary>
    /// The value the bits themselves hold, in a box bounded to the same bits so the two read as one column
    /// </summary>
    /// <remarks>
    /// This is the packed value rather than the data id, being what reading those bits alone gives. The floor that
    /// turns it into a data id belongs with the working in the value list, not here.
    /// </remarks>
    private void DrawValueBox(SKCanvas canvas, BitpackValueDetail value, SKRect bandBounds)
    {
        var top = bandBounds.Bottom + ValueBoxGap;

        var bounds = new SKRect(bandBounds.Left, top, bandBounds.Right, top + ValueBoxHeight);

        _fill.Color = ValueBoxColour;

        canvas.DrawRect(bounds, _fill);

        _stroke.Color = BorderColour;

        canvas.DrawRect(bounds, _stroke);

        var label = $"{value.Packed}";

        var textWidth = _monoFont.MeasureText(label);

        if (textWidth + 4f > bounds.Width)
        {
            return;
        }

        _text.Color = TextColour;

        canvas.DrawText(label,
                        bounds.MidX - (textWidth / 2),
                        bounds.Bottom - ((ValueBoxHeight - _monoFont.Size) / 2),
                        SKTextAlign.Left,
                        _monoFont,
                        _text);
    }

    private void DrawSelection(SKCanvas canvas, List<(SKRect Bounds, int Index)> regions)
    {
        foreach (var (bounds, index) in regions)
        {
            if (index != SelectedValueIndex)
            {
                continue;
            }

            _stroke.Color = SelectionColour;

            canvas.DrawRect(SKRect.Inflate(bounds, -0.5f, -0.5f), _stroke);

            return;
        }
    }

    /// <summary>
    /// Which end is which, the drawing running least significant bit first against the hex view's byte order
    /// </summary>
    private void DrawEndLabels(SKCanvas canvas, float left, float cellWidth, float width)
    {
        _text.Color = MutedColour;

        var baseline = Height - 3f;

        canvas.DrawText("LSB", left, baseline, SKTextAlign.Left, _labelFont, _text);

        var label = "MSB";

        canvas.DrawText(label,
                        left + (UnitBits * cellWidth) - _labelFont.MeasureText(label),
                        baseline,
                        SKTextAlign.Left,
                        _labelFont,
                        _text);
    }
}
