using System;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

internal static class OperatorLabelRenderer
{
    // A bar only gets a label when it is at least this tall and wide.
    private const float MinLabelBarHeight = 11f;
    private const float MinLabelBarWidth = 26f;

    private const float BarInset = 4f;

    private const float MaximumFont = 12f;
    private const float MinimumFont = 7f;
    private const float TwoLineGap = 2f;
    private const float LabelGapFraction = 0.5f;

    /// <summary>
    /// Draws the operator label based on available height/width
    /// </summary>
    public static void Draw(SKCanvas canvas,
                            ReadOnlySpan<char> type,
                            ReadOnlySpan<char> target,
                            float barLeft,
                            float barRight,
                            float y,
                            float barHeight,
                            SKFont boldFont,
                            SKFont font,
                            SKPaint text)
    {
        if (barHeight < MinLabelBarHeight || barRight - barLeft < MinLabelBarWidth || (type.Length == 0 && target.Length == 0))
        {
            return;
        }

        var startX = barLeft + BarInset;

        var endX = barRight - BarInset;

        const float textPadX = 14f;

        var availableWidth = endX - startX - textPadX * 2;

        if (availableWidth <= 0)
        {
            return;
        }

        // Widths scale linearly with font size, so measure once at the cap and derive each layout's width-bound size.
        boldFont.Size = MaximumFont;
        font.Size = MaximumFont;

        var typeWidth = type.Length > 0 ? boldFont.MeasureText(type) : 0f;
        var targetWidth = target.Length > 0 ? font.MeasureText(target) : 0f;

        var hasBoth = type.Length > 0 && target.Length > 0;

        // Two lines: type above the object name, left aligned
        if (hasBoth)
        {
            var widerAtMax = Math.Max(typeWidth, targetWidth);

            var sizeByWidth = widerAtMax <= 0 ? MaximumFont : MaximumFont * availableWidth / widerAtMax;

            var sizeByHeight = (barHeight - 2f - TwoLineGap) / 2f;

            var size = Math.Min(MaximumFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= MinimumFont)
            {
                boldFont.Size = size;
                font.Size = size;

                var x = startX + textPadX;
                var halfGap = (size + TwoLineGap) / 2f;

                DrawTextSpan(canvas, type, x, y - halfGap + size * 0.35f, boldFont, text);
                DrawTextSpan(canvas, target, x, y + halfGap + size * 0.35f, font, text);
                return;
            }
        }

        // Single line: type then object name left aligned
        if (hasBoth)
        {
            var widthAtMax = typeWidth + targetWidth + MaximumFont * LabelGapFraction;

            var sizeByWidth = widthAtMax <= 0 ? MaximumFont : MaximumFont * availableWidth / widthAtMax;

            var sizeByHeight = barHeight - 2f;

            var size = Math.Min(MaximumFont, Math.Min(sizeByWidth, sizeByHeight));

            if (size >= MinimumFont)
            {
                boldFont.Size = size;
                font.Size = size;

                var baseline = y + size * 0.35f;
                var x = startX + textPadX;

                DrawTextSpan(canvas, type, x, baseline, boldFont, text);

                x += boldFont.MeasureText(type, null) + size * LabelGapFraction;

                DrawTextSpan(canvas, target, x, baseline, font, text);

                return;
            }
        }

        // Single line: the operator type alone (or whichever single label is present)
        var isPrimaryType = type.Length > 0;
        var primarySpan = isPrimaryType ? type : target;
        var primaryFont = isPrimaryType ? boldFont : font;
        var primaryWAtMax = isPrimaryType ? typeWidth : targetWidth;

        var nameSizeByWidth = primaryWAtMax <= 0 ? MaximumFont : MaximumFont * availableWidth / primaryWAtMax;
        var nameSize = Math.Min(MaximumFont, Math.Min(nameSizeByWidth, barHeight - 2f));

        if (nameSize >= MinimumFont)
        {
            primaryFont.Size = nameSize;
            DrawTextSpan(canvas, primarySpan, startX + textPadX, y + nameSize * 0.35f, primaryFont, text);
        }
    }

    private static void DrawTextSpan(SKCanvas canvas,
                                     ReadOnlySpan<char> text,
                                     float x,
                                     float y,
                                     SKFont font,
                                     SKPaint paint)
    {
        using var blob = SKTextBlob.Create(text, font, SKPoint.Empty);

        if (blob is not null)
        {
            canvas.DrawText(blob, x, y, paint);
        }
    }
}
