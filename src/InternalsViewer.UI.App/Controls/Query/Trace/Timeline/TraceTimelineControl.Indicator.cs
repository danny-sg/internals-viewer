using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float IndicatorTextWidth = 320f;

    private const double IndicatorFontSize = 12;

    private const string IndicatorCodeFont = "Consolas";


    private const float IndicatorPadding = 12f;

    private const float IndicatorGap = 8f;

    private const float IndicatorArrowLength = 28f;

    private const float IndicatorArrowHead = 12f;

    private const float IndicatorLineHeight = 17f;

    private const float IndicatorButtonHeight = 24f;

    private const float IndicatorButtonPadding = 10f;

    private const float IndicatorCloseSize = 6f;

    private const float IndicatorCloseHit = 18f;

    private const int IndicatorHistory = 16;

    private const string IndicatorNextLabel = "Next";

    private const string IndicatorFinishLabel = "Finish";

    private readonly List<(Guid Id, TraceIndicatorResult Result)> _closedIndicators = [];

    private readonly SKPath _indicatorHead = new();

    private readonly MarkdownTextBlock _indicatorText = new()
    {
        IsHitTestVisible = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        MaxWidth = IndicatorTextWidth,
        Visibility = Visibility.Collapsed,
        FontSize = IndicatorFontSize,
        Resources =
        {
            [typeof(TextBlock)] = new Style(typeof(TextBlock))
            {
                Setters = { new Setter(TextBlock.FontFamilyProperty, new FontFamily(IndicatorCodeFont)) }
            }
        },
        Config = new MarkdownConfig
        {
            Themes = new MarkdownThemes
            {
                ParagraphMargin = new Thickness(0),
                InlineCodeFontSize = IndicatorFontSize,
                InlineCodeBackground = new SolidColorBrush(Colors.Gainsboro),
                InlineCodeForeground = new SolidColorBrush(Color.FromArgb(255, 0x1F, 0x1F, 0x1F)),
                InlineCodeBorderThickness = new Thickness(0),
                InlineCodePadding = new Thickness(3, 0, 3, 1)
            }
        }
    };

    private SKSize _indicatorTextSize;

    private OpenIndicator? _indicator;

    private IndicatorLayout? _indicatorLayout;

    private SKPoint? _indicatorPosition;

    private SKPoint? _indicatorGrab;

    public TraceIndicator? Indicator => _indicator?.Indicator;

    public int IndicatorStep => _indicator?.Step ?? 0;

    public void ShowIndicator(TraceIndicator indicator)
    {
        CloseIndicator(TraceIndicatorClose.Replaced);

        if (indicator.Steps.Length == 0)
        {
            return;
        }

        _indicator = new OpenIndicator(indicator);

        SetIndicatorText(indicator.Steps[0].Comment);

        RevealIndicator(indicator.Steps[0]);

        _canvas.Invalidate();
    }

    public bool CloseIndicator(TraceIndicatorClose reason)
    {
        if (_indicator is not { } open)
        {
            return false;
        }

        _indicator = null;

        _indicatorLayout = null;

        _indicatorText.Visibility = Visibility.Collapsed;

        _indicatorGrab = null;

        if (reason != TraceIndicatorClose.Next)
        {
            _indicatorPosition = null;
        }

        var result = new TraceIndicatorResult(reason, open.Step, open.Indicator.Steps.Length);

        _closedIndicators.Add((open.Indicator.Id, result));

        if (_closedIndicators.Count > IndicatorHistory)
        {
            _closedIndicators.RemoveAt(0);
        }

        open.Closed.TrySetResult(result);

        _canvas.Invalidate();

        return true;
    }

    public Task<TraceIndicatorResult>? GetIndicatorClose(Guid id)
    {
        if (_indicator is { } open && open.Indicator.Id == id)
        {
            return open.Closed.Task;
        }

        foreach (var (closedId, result) in _closedIndicators)
        {
            if (closedId == id)
            {
                return Task.FromResult(result);
            }
        }

        return null;
    }

    private void NextIndicatorStep()
    {
        if (_indicator is not { } open)
        {
            return;
        }

        if (open.Step >= open.Indicator.Steps.Length - 1)
        {
            CloseIndicator(TraceIndicatorClose.Next);

            return;
        }

        open.Step++;

        SetIndicatorText(open.Indicator.Steps[open.Step].Comment);

        RevealIndicator(open.Indicator.Steps[open.Step]);

        _canvas.Invalidate();
    }

    private void RevealIndicator(TraceIndicatorStep step)
    {
        double? start = null;

        double? end = null;

        FlameHit? firstHit = null;

        foreach (var target in step.Targets)
        {
            if (RevealSpan(target.Node, target.CallIndex) is not { } span)
            {
                continue;
            }

            var from = TraceScale.GetValue(span.From, _axis);

            var to = TraceScale.GetValue(span.To, _axis);

            start = Math.Min(start ?? from, from);

            end = Math.Max(end ?? to, to);

            firstHit ??= _visible is { } visible ? FindCall(visible, target.Node, target.CallIndex) : null;
        }

        if (firstHit is { } hit)
        {
            BringIntoView(hit);
        }

        if (start is { } first && end is { } last)
        {
            ZoomTo(first, last);
        }
    }

    private bool PressIndicator(SKPoint point)
    {
        switch (GetIndicatorHit(point))
        {
            case TraceIndicatorHit.Next:
                NextIndicatorStep();

                return true;

            case TraceIndicatorHit.Close:
                CloseIndicator(TraceIndicatorClose.Stopped);

                return true;

            case TraceIndicatorHit.Body when _indicatorLayout is { } layout:
                _indicatorGrab = new SKPoint(point.X - layout.Callout.Left, point.Y - layout.Callout.Top);

                return true;

            default:
                return false;
        }
    }

    private bool DragIndicator(SKPoint point)
    {
        if (_indicatorGrab is not { } grab)
        {
            return false;
        }

        _indicatorPosition = new SKPoint(point.X - grab.X, point.Y - grab.Y);

        _canvas.Invalidate();

        return true;
    }

    private bool ReleaseIndicator()
    {
        var isDragging = _indicatorGrab is not null;

        _indicatorGrab = null;

        return isDragging;
    }

    private TraceIndicatorHit? GetIndicatorHit(SKPoint point)
    {
        if (_indicatorLayout is not { } layout)
        {
            return null;
        }

        if (layout.Next is { } next && next.Contains(point))
        {
            return TraceIndicatorHit.Next;
        }

        if (layout.Close.Contains(point))
        {
            return TraceIndicatorHit.Close;
        }

        return layout.Callout.Contains(point) ? TraceIndicatorHit.Body : null;
    }

    private void DrawIndicator(SKCanvas canvas, int width, int height)
    {
        _indicatorLayout = null;

        if (_indicator is not { } open || !ShowFlameChart)
        {
            _indicatorText.Visibility = Visibility.Collapsed;

            return;
        }

        var step = open.Indicator.Steps[open.Step];

        var area = new SKRect(0, ContentTop, width, height - BandHeight);

        var anchors = area.Height > 0 ? GetIndicatorAnchors(step, area, width) : [];

        if (anchors.Count == 0)
        {
            _indicatorText.Visibility = Visibility.Collapsed;

            return;
        }

        var font = _paints.IndicatorFont;

        var textWidth = _indicatorTextSize.Width + IndicatorCloseSize + IndicatorPadding;

        var isLast = open.Step == open.Indicator.Steps.Length - 1;

        var button = step.HasNext ? IndicatorNextLabel : isLast && open.Indicator.Steps.Length > 1 ? IndicatorFinishLabel : null;

        var buttonWidth = button is null ? 0 : font.MeasureText(button) + IndicatorButtonPadding * 2;

        var counter = open.Indicator.Steps.Length > 1 ? $"{open.Step + 1} of {open.Indicator.Steps.Length}" : null;

        var counterWidth = counter is null ? 0 : font.MeasureText(counter) + IndicatorGap;

        var footer = button is not null || counter is not null ? IndicatorButtonHeight + IndicatorGap : 0;

        var size = new SKSize(Math.Max(textWidth, counterWidth + buttonWidth) + IndicatorPadding * 2,
                              _indicatorTextSize.Height + IndicatorPadding * 2 + footer);

        var placed = TraceIndicatorLayout.GetDefault(size,
                                                     [.. anchors.Select(a => (a.Top, a.Bottom))],
                                                     _rowHeight,
                                                     area,
                                                     IndicatorGap,
                                                     IndicatorArrowLength);

        var callout = SnapToPixels(_indicatorPosition is { } position ? TraceIndicatorLayout.Keep(position, size, area) : placed);

        canvas.Save();

        canvas.ClipRect(area);

        var arrows = new List<(SKPoint From, SKPoint To, string? Label)>(anchors.Count);

        foreach (var anchor in anchors)
        {
            if (TraceIndicatorLayout.GetArrow(callout, anchor.Top, anchor.Bottom, IndicatorPadding) is var (from, to))
            {
                arrows.Add((from, to, anchor.Label));
            }
        }

        canvas.SaveLayer(_paints.IndicatorArrowShadow);

        foreach (var (from, to, _) in arrows)
        {
            DrawIndicatorArrow(canvas, from, to);
        }

        canvas.Restore();

        foreach (var (from, to, label) in arrows)
        {
            if (label is { Length: > 0 })
            {
                DrawIndicatorLabel(canvas, label, to, to.Y >= from.Y, width);
            }
        }

        canvas.DrawRect(callout, _paints.IndicatorFill);

        canvas.DrawRect(GetBorder(callout), _paints.IndicatorEdge);

        _indicatorText.Translation = new Vector3(callout.Left + IndicatorPadding, callout.Top + IndicatorPadding, 0);

        _indicatorText.Visibility = Visibility.Visible;

        var close = DrawIndicatorClose(canvas, callout);

        if (counter is not null)
        {
            canvas.DrawText(counter,
                            callout.Left + IndicatorPadding,
                            IndicatorBaseline(font,
                                              callout.Bottom - IndicatorPadding - (IndicatorButtonHeight + IndicatorLineHeight) / 2),
                            SKTextAlign.Left,
                            font,
                            _paints.Label);
        }

        SKRect? next = null;

        if (button is not null)
        {
            var buttonArea = SnapToPixels(SKRect.Create(callout.Right - IndicatorPadding - buttonWidth,
                                                        callout.Bottom - IndicatorPadding - IndicatorButtonHeight,
                                                        buttonWidth,
                                                        IndicatorButtonHeight));

            canvas.DrawRect(buttonArea, _paints.IndicatorButton);

            canvas.DrawText(button,
                            buttonArea.MidX,
                            IndicatorBaseline(font, buttonArea.Top + (IndicatorButtonHeight - IndicatorLineHeight) / 2),
                            SKTextAlign.Center,
                            font,
                            _paints.IndicatorButtonText);

            next = buttonArea;
        }

        canvas.Restore();

        _indicatorLayout = new IndicatorLayout(callout, close, next);
    }

    private void SetIndicatorText(string comment)
    {
        _indicatorText.Text = comment;

        _indicatorText.Visibility = Visibility.Visible;

        _indicatorText.Measure(new Size(IndicatorTextWidth, double.PositiveInfinity));

        _indicatorTextSize = GetSize(_indicatorText.DesiredSize);
    }

    private void OnIndicatorTextSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _indicatorTextSize = GetSize(e.NewSize);

        _canvas.Invalidate();
    }

    private List<IndicatorAnchor> GetIndicatorAnchors(TraceIndicatorStep step, SKRect area, int width)
    {
        var anchors = new List<IndicatorAnchor>(step.Targets.Length);

        var pixelsPerUnit = PixelsPerUnit(width);

        foreach (var target in step.Targets)
        {
            if (GetCall(target.Node, target.CallIndex) is not { } marked)
            {
                continue;
            }

            var left = (float)((TraceScale.GetValue(marked.Span.From, _axis) - _viewStart) * pixelsPerUnit);

            var right = (float)((TraceScale.GetValue(marked.Span.To, _axis) - _viewStart) * pixelsPerUnit);

            var x = Math.Clamp((Math.Max(left, 0) + Math.Min(right, width)) / 2, 0, width);

            var top = GetMarkedTop(marked) is { } rowTop ? Math.Clamp(rowTop, area.Top, area.Bottom) : area.Top;

            anchors.Add(new IndicatorAnchor(new SKPoint(x, top), Math.Min(top + _rowHeight, area.Bottom), target.Label));
        }

        return anchors;
    }

    private void DrawIndicatorArrow(SKCanvas canvas, SKPoint from, SKPoint to)
    {
        var length = SKPoint.Distance(from, to);

        if (length < 1)
        {
            return;
        }

        var directionX = (to.X - from.X) / length;

        var directionY = (to.Y - from.Y) / length;

        var baseX = to.X - directionX * IndicatorArrowHead;

        var baseY = to.Y - directionY * IndicatorArrowHead;

        if (length > IndicatorArrowHead)
        {
            canvas.DrawLine(from.X, from.Y, baseX + directionX, baseY + directionY, _paints.IndicatorArrow);
        }

        _indicatorHead.Reset();

        _indicatorHead.MoveTo(to);

        _indicatorHead.LineTo(baseX - directionY * IndicatorArrowHead / 2, baseY + directionX * IndicatorArrowHead / 2);

        _indicatorHead.LineTo(baseX + directionY * IndicatorArrowHead / 2, baseY - directionX * IndicatorArrowHead / 2);

        _indicatorHead.Close();

        canvas.DrawPath(_indicatorHead, _paints.IndicatorHead);
    }

    private void DrawIndicatorLabel(SKCanvas canvas, string label, SKPoint at, bool isAbove, int width)
    {
        var font = _paints.IndicatorFont;

        var textWidth = font.MeasureText(label) + IndicatorPadding;

        var left = Math.Clamp(at.X + IndicatorArrowHead, 0, Math.Max(0, width - textWidth));

        var top = isAbove ? at.Y - IndicatorLineHeight - IndicatorArrowHead : at.Y + IndicatorArrowHead;

        canvas.DrawRect(SKRect.Create(left, top, textWidth, IndicatorLineHeight), _paints.LabelBackground);

        canvas.DrawText(label, left + IndicatorPadding / 2, IndicatorBaseline(font, top), SKTextAlign.Left, font, _paints.Label);
    }

    private SKRect DrawIndicatorClose(SKCanvas canvas, SKRect callout)
    {
        var right = callout.Right - IndicatorPadding;

        var top = callout.Top + IndicatorPadding;

        canvas.DrawLine(right - IndicatorCloseSize, top, right, top + IndicatorCloseSize, _paints.IndicatorCross);

        canvas.DrawLine(right, top, right - IndicatorCloseSize, top + IndicatorCloseSize, _paints.IndicatorCross);

        var centreX = right - IndicatorCloseSize / 2;

        var centreY = top + IndicatorCloseSize / 2;

        return SKRect.Create(centreX - IndicatorCloseHit / 2, centreY - IndicatorCloseHit / 2, IndicatorCloseHit, IndicatorCloseHit);
    }

    private static SKPoint ToSkPoint(Point point) => new((float)point.X, (float)point.Y);

    private static SKSize GetSize(Size size) => new((float)Math.Ceiling(size.Width), (float)Math.Ceiling(size.Height));

    private static SKRect SnapToPixels(SKRect rect)
        => new(MathF.Round(rect.Left), MathF.Round(rect.Top), MathF.Round(rect.Right), MathF.Round(rect.Bottom));

    private static SKRect GetBorder(SKRect rect) => new(rect.Left + 0.5f, rect.Top + 0.5f, rect.Right - 0.5f, rect.Bottom - 0.5f);

    private static float IndicatorBaseline(SKFont font, float top)
    {
        var metrics = font.Metrics;

        return top + (IndicatorLineHeight - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private readonly record struct IndicatorAnchor(SKPoint Top, float Bottom, string? Label);

    private sealed record IndicatorLayout(SKRect Callout, SKRect Close, SKRect? Next);

    private sealed class OpenIndicator(TraceIndicator indicator)
    {
        public TraceIndicator Indicator { get; } = indicator;

        public TaskCompletionSource<TraceIndicatorResult> Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Step { get; set; }
    }
}
