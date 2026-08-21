using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Draws the bit walk one entry decodes through, each code led down to what it stands for
/// </summary>
/// <remarks>
/// Only the words the entry actually reaches are drawn, whole or in part, so the drawing is the entry rather than the page around it.
///
/// A code is a handful of bits and what it decodes to needs far more room than that, so the codes are drawn at their real width and the
/// boxes are spread out beneath them, joined by a leader. The codes vary in width because that is the whole point of the coding - a common
/// character costs fewer bits than a rare one.
/// </remarks>
public sealed class HuffmanDecodeRenderer : IDisposable
{
    private const int BitsPerWord = 16;

    private const float WordHeight = 20f;

    private const float BitHeight = 22f;

    private const float LeaderHeight = 26f;

    private const float BoxLineHeight = 15f;

    private const float BoxPadding = 4f;

    private const float BoxGap = 10f;

    private const float BitWidth = 13f;

    /// <summary>
    /// Symbol in binary, then in hex, then as a character where it prints as one
    /// </summary>
    private const int BoxLines = 3;

    private static float BoxHeight => (BoxLines * BoxLineHeight) + (BoxPadding * 2);

    /// <summary>
    /// The whole drawing, there being only one row of codes however long the entry runs
    /// </summary>
    public static float Height
        => WordHeight + BitHeight + LeaderHeight + BoxHeight + (ColumnstoreLayout.Margin * 2);

    private readonly SKPaint _fill = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    private readonly SKPaint _stroke = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKPaint _leader = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private static readonly SKTypeface MonoTypeface = SKTypeface.FromFamilyName("Cascadia Mono") ?? SKTypeface.Default;

    private readonly SKFont _font = new(MonoTypeface, 11f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    public SKColor TextColour { get; set; } = new(0x20, 0x20, 0x20);

    public SKColor MutedColour { get; set; } = new(0x70, 0x70, 0x70);

    /// <summary>
    /// Bits of the words the entry does not reach, which belong to the entries either side of it
    /// </summary>
    public SKColor UnusedColour { get; set; } = new(0xBB, 0xB9, 0xB2);

    public SKColor BorderColour { get; set; } = new(0xD0, 0xCE, 0xC6);

    public SKColor WordBoxColour { get; set; } = new(0xF0, 0xEF, 0xEA);

    public SKColor BoxColour { get; set; } = new(0xFA, 0xF9, 0xF6);

    /// <summary>
    /// Two tints alternating along the walk, so where one code ends and the next begins is never in doubt
    /// </summary>
    /// <remarks>
    /// Green rather than the blue the bit pack ruler uses, the two being different codings that happen to share a drawing.
    /// </remarks>
    public SKColor[] BandColours { get; set; } = [new(0xDC, 0xED, 0xC8), new(0xC5, 0xE1, 0xA5)];

    /// <summary>
    /// The length prefix is drawn apart from the content, being the rule that decides how far the walk runs
    /// </summary>
    public SKColor LengthColour { get; set; } = new(0xFF, 0xE0, 0xB2);

    public SKColor SelectionColour { get; set; } = new(0x2E, 0x7D, 0x32);

    public int SelectedStep { get; set; } = -1;

    private (SKPoint From, SKPoint To)? _selectedLeader;

    /// <summary>
    /// The selection over the top of everything, a neighbouring fill otherwise painting out the side of its border
    /// </summary>
    private void DrawSelection(SKCanvas canvas, List<(SKRect Bounds, int Index)> regions)
    {
        if (SelectedStep < 0)
        {
            return;
        }

        _stroke.Color = SelectionColour;

        foreach (var (bounds, index) in regions)
        {
            if (index == SelectedStep)
            {
                canvas.DrawRect(bounds, _stroke);
            }
        }

        if (_selectedLeader is { } leader)
        {
            _leader.Color = SelectionColour;

            canvas.DrawLine(leader.From, leader.To, _leader);
        }
    }

    private float BoxWidth => _font.MeasureText("00000000") + (BoxPadding * 4);

    /// <summary>
    /// Width the whole walk needs, the codes running on rather than wrapping
    /// </summary>
    /// <remarks>
    /// A code carries on from the one before it, so breaking the walk onto another line breaks the thing it is
    /// showing. It runs on instead and the drawing scrolls sideways.
    /// </remarks>
    public float GetWidth(IReadOnlyList<HuffmanDecodeStep> steps)
        => steps.Count == 0 ? 0 : GetInnerWidth(steps) + (ColumnstoreLayout.Margin * 2);

    private float GetInnerWidth(IReadOnlyList<HuffmanDecodeStep> steps)
        => Math.Max(GetBitsWidth(steps), (steps.Count * BoxWidth) + ((steps.Count - 1) * BoxGap));

    private static float GetBitsWidth(IReadOnlyList<HuffmanDecodeStep> steps)
    {
        var last = steps[^1];

        var firstBit = steps[0].BitOffset / BitsPerWord * BitsPerWord;

        var lastBit = (last.BitOffset + last.BitLength + BitsPerWord - 1) / BitsPerWord * BitsPerWord;

        return (lastBit - firstBit) * BitWidth;
    }

    public List<(SKRect Bounds, int Index)> Draw(SKCanvas canvas,
                                                 IReadOnlyList<HuffmanDecodeStep> steps,
                                                 ReadOnlyMemory<byte> content,
                                                 float width,
                                                 float scrollOffset)
    {
        var regions = new List<(SKRect, int)>();

        if (steps.Count == 0)
        {
            return regions;
        }

        _selectedLeader = null;

        var top = ColumnstoreLayout.Margin;

        var count = steps.Count;

        var inner = GetInnerWidth(steps);

        // Centred while it fits, pinned to the margin and scrolled once it does not
        var origin = Math.Max(ColumnstoreLayout.Margin, (width - inner) / 2) - scrollOffset;

        var bitsWidth = GetBitsWidth(steps);

        var bitsLeft = origin + ((inner - bitsWidth) / 2);

        var boxesLeft = origin + ((inner - ((count * BoxWidth) + ((count - 1) * BoxGap))) / 2);

        // Words run whole even where the codes only reach part of them, so the run is widened out to word boundaries
        var firstBit = steps[0].BitOffset / BitsPerWord * BitsPerWord;

        var lastStep = steps[^1];

        var lastBit = (lastStep.BitOffset + lastStep.BitLength + BitsPerWord - 1) / BitsPerWord * BitsPerWord;

        DrawWords(canvas, content, bitsLeft, top, firstBit, lastBit);

        var codes = new SKRect[count];

        for (var i = 0; i < count; i++)
        {
            codes[i] = new SKRect(bitsLeft + ((steps[i].BitOffset - firstBit) * BitWidth),
                                  top + WordHeight,
                                  bitsLeft + ((steps[i].BitOffset + steps[i].BitLength - firstBit) * BitWidth),
                                  top + WordHeight + BitHeight);

            DrawCode(canvas, steps[i], i, codes[i]);
        }

        // The bits go on last so the tints behind them do not paint over them
        DrawBits(canvas, content, bitsLeft, top + WordHeight, firstBit, lastBit,
                 steps[0].BitOffset, lastStep.BitOffset + lastStep.BitLength);

        var boxes = new SKRect[count];

        for (var i = 0; i < count; i++)
        {
            var left = boxesLeft + (i * (BoxWidth + BoxGap));

            boxes[i] = new SKRect(left,
                                  top + WordHeight + BitHeight + LeaderHeight,
                                  left + BoxWidth,
                                  top + WordHeight + BitHeight + LeaderHeight + BoxHeight);

            // Leaders go down before any box does, so a later box never paints over an earlier leader
            DrawLeader(canvas, i, codes[i], boxes[i]);
        }

        for (var i = 0; i < count; i++)
        {
            DrawBox(canvas, steps[i], i, boxes[i]);

            regions.Add((codes[i], i));
            regions.Add((boxes[i], i));
        }

        DrawSelection(canvas, regions);

        return regions;
    }

    /// <summary>
    /// The words the codes fall in, shown a word at a time rather than a byte at a time
    /// </summary>
    /// <remarks>
    /// The stream is read as sixteen bit little endian words consumed most significant bit first, so a byte row would put the two halves of
    /// every word the wrong way round against the bits beneath it.
    /// </remarks>
    private void DrawWords(SKCanvas canvas, ReadOnlyMemory<byte> content, float left, float top, int firstBit, int lastBit)
    {
        for (var bit = firstBit; bit < lastBit; bit += BitsPerWord)
        {
            var bounds = new SKRect(left + ((bit - firstBit) * BitWidth),
                                    top,
                                    left + ((bit - firstBit + BitsPerWord) * BitWidth),
                                    top + WordHeight);

            _fill.Color = WordBoxColour;

            canvas.DrawRect(bounds, _fill);

            _stroke.Color = BorderColour;

            canvas.DrawRect(bounds, _stroke);

            if (TryReadWord(content, bit, out var word))
            {
                DrawCentred(canvas, $"0x{word:X4}", bounds, TextColour, WordHeight, -1f);
            }
        }
    }

    /// <summary>
    /// The bits of the words, with the ones outside the entry dimmed as belonging to its neighbours
    /// </summary>
    private void DrawBits(SKCanvas canvas,
                          ReadOnlyMemory<byte> content,
                          float left,
                          float top,
                          int firstBit,
                          int lastBit,
                          int codedFrom,
                          int codedTo)
    {
        _stroke.Color = BorderColour;

        canvas.DrawRect(new SKRect(left, top, left + ((lastBit - firstBit) * BitWidth), top + BitHeight), _stroke);

        for (var bit = firstBit; bit < lastBit; bit++)
        {
            if (!TryReadBit(content, bit, out var isSet))
            {
                continue;
            }

            var glyph = isSet ? "1" : "0";

            _text.Color = bit >= codedFrom && bit < codedTo ? TextColour : UnusedColour;

            canvas.DrawText(glyph,
                            left + ((bit - firstBit) * BitWidth) + ((BitWidth - _font.MeasureText(glyph)) / 2),
                            top + (BitHeight / 2) + (_font.Size / 2) - 1,
                            SKTextAlign.Left,
                            _font,
                            _text);
        }
    }

    /// <summary>
    /// One code's tint behind the bits it is made of
    /// </summary>
    private void DrawCode(SKCanvas canvas, HuffmanDecodeStep step, int index, SKRect bounds)
    {
        _fill.Color = step.IsLength ? LengthColour : BandColours[index % BandColours.Length];

        canvas.DrawRect(bounds, _fill);

        _stroke.Color = index == SelectedStep ? SelectionColour : BorderColour;

        canvas.DrawRect(bounds, _stroke);
    }

    private void DrawLeader(SKCanvas canvas, int index, SKRect code, SKRect box)
    {
        _leader.Color = BorderColour;

        canvas.DrawLine(code.MidX, code.Bottom, box.MidX, box.Top, _leader);

        if (index == SelectedStep)
        {
            _selectedLeader = (new SKPoint(code.MidX, code.Bottom), new SKPoint(box.MidX, box.Top));
        }
    }

    /// <summary>
    /// What the code stands for, as the symbol in binary, in hex, then as a character where it prints as one
    /// </summary>
    private void DrawBox(SKCanvas canvas, HuffmanDecodeStep step, int index, SKRect bounds)
    {
        _fill.Color = BoxColour;

        canvas.DrawRect(bounds, _fill);

        _stroke.Color = index == SelectedStep ? SelectionColour : BorderColour;

        canvas.DrawRect(bounds, _stroke);

        var colour = index == SelectedStep ? SelectionColour : TextColour;

        DrawLine(canvas, Convert.ToString(step.Symbol, 2).PadLeft(8, '0'), bounds, 0, colour);

        DrawLine(canvas, $"0x{step.Symbol:X2}", bounds, 1, colour);

        DrawLine(canvas, step.IsLength ? "Length" : step.Character, bounds, 2, MutedColour);

        _stroke.Color = BorderColour;

        for (var line = 1; line < BoxLines; line++)
        {
            var y = bounds.Top + BoxPadding + (line * BoxLineHeight);

            canvas.DrawLine(bounds.Left, y, bounds.Right, y, _stroke);
        }
    }

    private void DrawLine(SKCanvas canvas, string label, SKRect box, int line, SKColor colour)
    {
        if (label.Length == 0)
        {
            return;
        }

        var bounds = new SKRect(box.Left,
                                box.Top + BoxPadding + (line * BoxLineHeight),
                                box.Right,
                                box.Top + BoxPadding + ((line + 1) * BoxLineHeight));

        DrawCentred(canvas, label, bounds, colour, BoxLineHeight);
    }

    private void DrawCentred(SKCanvas canvas, string label, SKRect bounds, SKColor colour, float height, float lift = 0f)
    {
        var textWidth = _font.MeasureText(label);

        if (textWidth + 2f > bounds.Width)
        {
            return;
        }

        _text.Color = colour;

        canvas.DrawText(label,
                        bounds.MidX - (textWidth / 2),
                        bounds.Bottom - ((height - _font.Size) / 2) + lift,
                        SKTextAlign.Left,
                        _font,
                        _text);
    }

    /// <summary>
    /// The word a bit falls in, read little endian the same way the decoder pulls it in
    /// </summary>
    private static bool TryReadWord(ReadOnlyMemory<byte> content, int bitOffset, out ushort word)
    {
        word = 0;

        var offset = bitOffset / BitsPerWord * 2;

        if (offset < 0 || offset + 1 >= content.Length)
        {
            return false;
        }

        word = (ushort)(content.Span[offset] | (content.Span[offset + 1] << 8));

        return true;
    }

    /// <summary>
    /// Bits run from the most significant bit of the word down, which is the order the decoder consumes them
    /// </summary>
    private static bool TryReadBit(ReadOnlyMemory<byte> content, int bitOffset, out bool isSet)
    {
        isSet = false;

        if (!TryReadWord(content, bitOffset, out var word))
        {
            return false;
        }

        isSet = (word & (1 << (BitsPerWord - 1 - (bitOffset % BitsPerWord)))) != 0;

        return true;
    }
    /// <summary>
    /// Releases the paints and fonts, which hold native Skia handles rather than managed memory
    /// </summary>
    public void Dispose()
    {
        _fill.Dispose();
        _stroke.Dispose();
        _leader.Dispose();
        _text.Dispose();
        _font.Dispose();
    }
}
