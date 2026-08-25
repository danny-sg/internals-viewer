using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore.Dictionary;

/// <summary>
/// Draws a Huffman code table as the tree its codes describe, one row per symbol
/// </summary>
/// <remarks>
/// Laid out left to right by code length rather than as a balanced tree, so how far a symbol sits from the root is how many bits it costs.
///
/// A short row is a common symbol.
/// </remarks>
public sealed class HuffmanTreeRenderer : IDisposable
{
    public const float RowHeight = 29f;

    private const float LevelWidth = 30f;

    private const float LeafGap = 8f;

    private const float NodeRadius = 2.5f;

    private const float BitGap = 4f;

    private const float SelectedStrokeWidth = 2f;

    private static readonly SKTypeface MonoTypeface = SKTypeface.FromFamilyName("Cascadia Mono") ?? SKTypeface.Default;

    private static readonly string[] Labels = BuildLabels();

    private readonly SKPaint _line = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKPaint _dot = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKFont _font = new(MonoTypeface, 11f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    private readonly HashSet<HuffmanTreeNode> _path = [];

    private readonly float[] _labelWidths = new float[256];

    public SKColor TextColour { get; set; } = new(0x20, 0x20, 0x20);

    public SKColor MutedColour { get; set; } = new(0x70, 0x70, 0x70);

    public SKColor BranchColour { get; set; } = new(0xB0, 0xAE, 0xA6);

    public SKColor LeafColour { get; set; } = new(0x18, 0x5F, 0xA5);

    public SKColor SelectionColour { get; set; } = new(0x2E, 0x7D, 0x32);

    public int SelectedSymbol { get; set; } = -1;

    public static float GetHeight(HuffmanTreeNode root) => (root.LeafCount * RowHeight) + (ColumnstoreLayout.Margin * 2);

    public void Draw(SKCanvas canvas, HuffmanTreeNode root, float scrollOffset, float height)
    {
        BuildPath(root);

        var top = ColumnstoreLayout.Margin - scrollOffset;

        foreach (var node in root.Descend())
        {
            var y = top + (node.Row * RowHeight) + (RowHeight / 2);

            if (y < -RowHeight || y > height + RowHeight)
            {
                continue;
            }

            DrawNode(canvas, node, top, y);
        }
    }

    public static IReadOnlyList<(SKRect Bounds, int Symbol)> GetLeafRegions(HuffmanTreeNode root, float scrollOffset, float width)
    {
        var regions = new List<(SKRect, int)>();

        var top = ColumnstoreLayout.Margin - scrollOffset;

        foreach (var node in root.Descend())
        {
            if (!node.IsLeaf)
            {
                continue;
            }

            var y = top + (node.Row * RowHeight);

            regions.Add((new SKRect(0, y, width, y + RowHeight), node.Code!.Value.Symbol));
        }

        return regions;
    }

    /// <summary>
    /// Releases the paints and fonts, which hold native Skia handles rather than managed memory
    /// </summary>
    public void Dispose()
    {
        _line.Dispose();
        _dot.Dispose();
        _text.Dispose();
        _font.Dispose();
    }

    /// <summary>
    /// Collects the nodes between the root and the selected symbol, which are drawn as the route its bits take
    /// </summary>
    private void BuildPath(HuffmanTreeNode root)
    {
        _path.Clear();

        if (SelectedSymbol < 0)
        {
            return;
        }

        Follow(root);
    }

    private bool Follow(HuffmanTreeNode node)
    {
        _path.Add(node);

        if (node.IsLeaf)
        {
            if (node.Code!.Value.Symbol == SelectedSymbol)
            {
                return true;
            }
        }
        else if ((node.Zero is { } zero && Follow(zero)) || (node.One is { } one && Follow(one)))
        {
            return true;
        }

        _path.Remove(node);

        return false;
    }

    private void DrawNode(SKCanvas canvas, HuffmanTreeNode node, float top, float y)
    {
        var x = ColumnstoreLayout.Margin + (node.Depth * LevelWidth);

        DrawEdges(canvas, node, top, x, y);

        if (node.IsLeaf)
        {
            DrawLeaf(canvas, node, x, y);

            return;
        }

        _dot.Color = _path.Contains(node) ? SelectionColour : BranchColour;

        canvas.DrawCircle(x, y, NodeRadius, _dot);
    }

    /// <summary>
    /// The elbow down to each child, drawn from the parent so a child never has to know where its parent sits
    /// </summary>
    private void DrawEdges(SKCanvas canvas, HuffmanTreeNode node, float top, float x, float y)
    {
        if (node.Zero is { } zero)
        {
            DrawEdge(canvas, node, zero, "0", top, x, y);
        }

        if (node.One is { } one)
        {
            DrawEdge(canvas, node, one, "1", top, x, y);
        }

        _line.StrokeWidth = 1;
    }

    private void DrawEdge(SKCanvas canvas,
                          HuffmanTreeNode node,
                          HuffmanTreeNode child,
                          string bit,
                          float top,
                          float x,
                          float y)
    {
        var childY = top + (child.Row * RowHeight) + (RowHeight / 2);

        var childX = ColumnstoreLayout.Margin + (child.Depth * LevelWidth);

        var isOnPath = _path.Contains(node) && _path.Contains(child);

        _line.Color = isOnPath ? SelectionColour : BranchColour;
        _line.StrokeWidth = isOnPath ? SelectedStrokeWidth : 1;

        canvas.DrawLine(x, y, x, childY, _line);
        canvas.DrawLine(x, childY, childX, childY, _line);

        _text.Color = isOnPath ? SelectionColour : MutedColour;

        canvas.DrawText(bit,
                        childX - BitGap,
                        childY - BitGap,
                        SKTextAlign.Right,
                        _font,
                        _text);
    }

    private void DrawLeaf(SKCanvas canvas, HuffmanTreeNode node, float x, float y)
    {
        var code = node.Code!.Value;

        var isSelected = code.Symbol == SelectedSymbol;

        _dot.Color = isSelected ? SelectionColour : LeafColour;

        canvas.DrawCircle(x, y, NodeRadius + 1, _dot);

        var label = Label(code.Symbol);

        _text.Color = isSelected ? SelectionColour : TextColour;

        canvas.DrawText(label, x + LeafGap, y + (_font.Size / 2) - 1, SKTextAlign.Left, _font, _text);

        _text.Color = MutedColour;

        canvas.DrawText(code.Bits,
                        x + LeafGap + GetLabelWidth(code.Symbol, label) + LeafGap,
                        y + (_font.Size / 2) - 1,
                        SKTextAlign.Left,
                        _font,
                        _text);
    }

    private float GetLabelWidth(int symbol, string label)
    {
        if ((uint)symbol >= 256)
        {
            return _font.MeasureText(label);
        }

        if (_labelWidths[symbol] <= 0)
        {
            _labelWidths[symbol] = _font.MeasureText(label);
        }

        return _labelWidths[symbol];
    }

    private static string[] BuildLabels()
    {
        var labels = new string[256];

        for (var symbol = 0; symbol < labels.Length; symbol++)
        {
            labels[symbol] = symbol is >= 0x20 and < 0x7F ? $"'{(char)symbol}'" : $"0x{symbol:X2}";
        }

        return labels;
    }

    /// <summary>
    /// A printable symbol shows as itself, everything else as the byte it codes
    /// </summary>
    private static string Label(int symbol)
        => (uint)symbol < 256
            ? Labels[symbol]
            : symbol is >= 0x20 and < 0x7F ? $"'{(char)symbol}'" : $"0x{symbol:X2}";
}
