using System;
using InternalsViewer.Query.Helpers;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Timeline;
using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Timeline;

public sealed class TimelineRuler : UserControl, IDisposable
{
    private const string PinText = "Pin to Top";

    private const string UnpinText = "Unpin";

    private const float RulerHeight = 18f;

    private const float StripHeight = 16f;

    private const float PlayheadHalfWidth = 9f;

    private const float PlayheadBadgePadding = 4f;

    private const float BadgeRadius = 2f;

    private const float TickSpacing = 110f;

    private const float TickLength = 4f;

    private const float LabelGap = 4f;

    private const float HandleWidth = 7f;

    private const float HandleHeight = 8f;

    private const float HandleGap = PlayheadHalfWidth + 4f;

    private const float HitWidth = 6f;

    private const long DoubleClickMilliseconds = 300;

    private const double EdgeZone = 24;

    private const double EdgeScrollPixels = 16;

    private const int EdgeScrollMilliseconds = 16;

    private const double MarkHalfPixel = 0.5;

    private static readonly SKColor HandleColour = new(128, 128, 128);

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source),
                                    typeof(ITimelineRulerSource),
                                    typeof(TimelineRuler),
                                    new PropertyMetadata(null, OnSourceChanged));

    public ITimelineRulerSource? Source
    {
        get => (ITimelineRulerSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(nameof(IsPinned),
                                    typeof(bool),
                                    typeof(TimelineRuler),
                                    new PropertyMetadata(true));

    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public TimelineRuler()
    {
        Canvas = new SKXamlCanvas { IgnorePixelScaling = false };

        Canvas.PaintSurface += OnPaintSurface;

        Content = new Grid { Background = new SolidColorBrush(Colors.Transparent), Children = { Canvas } };

        Height = RulerHeight + StripHeight;

        Loaded += OnLoaded;

        ActualThemeChanged += OnActualThemeChanged;

        PointerPressed += OnPointerPressed;

        PointerMoved += OnPointerMoved;

        PointerReleased += OnPointerReleased;

        PointerCaptureLost += OnPointerCaptureLost;

        ContextRequested += OnContextRequested;

        EdgeTimer = DispatcherQueue.CreateTimer();

        EdgeTimer.Interval = TimeSpan.FromMilliseconds(EdgeScrollMilliseconds);

        EdgeTimer.IsRepeating = true;

        EdgeTimer.Tick += OnEdgeTick;
    }

    private SKXamlCanvas Canvas { get; }

    private DispatcherQueueTimer EdgeTimer { get; }

    private double EdgePointerX { get; set; }

    private TraceTimelinePaints Paints { get; } = new();

    private SKPath Triangle { get; } = CreateTriangle();

    private SKPath StartHandle { get; } = CreateHandle(isStart: true);

    private SKPath EndHandle { get; } = CreateHandle(isStart: false);

    private RulerDrag Drag { get; set; }

    private long LastPressTicks { get; set; }

    private double LastPressX { get; set; }

    private string? HoverText { get; set; }

    public static string GetPinText(bool isPinned) => isPinned ? UnpinText : PinText;

    public static double? GetContentOffset(FrameworkElement content, UIElement relativeTo)
    {
        if (!content.IsLoaded || content.ActualWidth <= 0 || content.XamlRoot != relativeTo.XamlRoot)
        {
            return null;
        }

        try
        {
            return content.TransformToVisual(relativeTo).TransformPoint(default).X;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        EdgeTimer.Stop();

        EdgeTimer.Tick -= OnEdgeTick;

        Source = null;

        Loaded -= OnLoaded;

        ActualThemeChanged -= OnActualThemeChanged;

        PointerPressed -= OnPointerPressed;

        PointerMoved -= OnPointerMoved;

        PointerReleased -= OnPointerReleased;

        PointerCaptureLost -= OnPointerCaptureLost;

        ContextRequested -= OnContextRequested;

        Canvas.PaintSurface -= OnPaintSurface;

        Paints.Dispose();

        Triangle.Dispose();

        StartHandle.Dispose();

        EndHandle.Dispose();
    }

    private static SKPath CreateTriangle()
    {
        using var builder = new SKPathBuilder();

        builder.MoveTo(0, RulerHeight + StripHeight);

        builder.LineTo(-PlayheadHalfWidth, RulerHeight);

        builder.LineTo(PlayheadHalfWidth, RulerHeight);

        builder.Close();

        return builder.Detach();
    }

    private static SKPath CreateHandle(bool isStart)
    {
        using var builder = new SKPathBuilder();

        var half = HandleWidth / 2;

        builder.MoveTo(-half, 0);

        builder.LineTo(half, 0);

        builder.LineTo(isStart ? -half : half, HandleHeight);

        builder.Close();

        return builder.Detach();
    }

    private TimelineRulerArea GetArea(ITimelineRulerSource source, TimelineRulerState state)
        => source.GetRulerOffset(this) is { } offset
            ? new TimelineRulerArea(offset + state.ContentLeft, offset + state.ContentRight)
            : new TimelineRulerArea(state.ContentLeft, ActualWidth);

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyTheme();

    private void OnActualThemeChanged(FrameworkElement sender, object args) => ApplyTheme();

    private void ApplyTheme()
    {
        Paints.Apply(ActualTheme == ElementTheme.Dark);

        Canvas.Invalidate();
    }

    private void OnSourceRulerChanged() => Canvas.Invalidate();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed || Source is not { } source || source.GetRulerState() is not { } state)
        {
            return;
        }

        var position = point.Position;

        var area = GetArea(source, state);

        var now = Environment.TickCount64;

        var isDoubleClick = now - LastPressTicks <= DoubleClickMilliseconds && Math.Abs(position.X - LastPressX) <= HitWidth;

        LastPressTicks = now;

        LastPressX = position.X;

        e.Handled = true;

        if (isDoubleClick
            && state.Playhead is { } playhead
            && Math.Abs(TimelineRulerLayout.GetX(state, area, playhead) - position.X) <= PlayheadHalfWidth)
        {
            source.ResetRulerCrop();

            return;
        }

        Drag = GetDragAt(state, area, position.X, position.Y);

        if (Drag == RulerDrag.Playhead && ClickMark(source, state, area, position))
        {
            Drag = RulerDrag.None;

            return;
        }

        CapturePointer(e.Pointer);

        if (Drag == RulerDrag.Playhead)
        {
            source.MoveRulerPlayhead(TimelineRulerLayout.GetValue(state, area, Math.Clamp(position.X, area.Left, area.Right)));

            UpdateEdgeScroll(position.X);
        }
    }

    private RulerDrag GetDragAt(TimelineRulerState state, TimelineRulerArea area, double x, double y)
    {
        if (y < ActualHeight - HandleHeight - HitWidth)
        {
            return RulerDrag.Playhead;
        }

        var (startX, endX) = TimelineRulerLayout.GetHandleXs(state, area, HandleGap);

        var toStart = startX is { } start ? Math.Abs(x - start) : double.MaxValue;

        var toEnd = endX is { } end ? Math.Abs(x - end) : double.MaxValue;

        if (toStart <= HitWidth && toStart <= toEnd)
        {
            return RulerDrag.Start;
        }

        return toEnd <= HitWidth ? RulerDrag.End : RulerDrag.Playhead;
    }

    private bool ClickMark(ITimelineRulerSource source, TimelineRulerState state, TimelineRulerArea area, Point position)
    {
        if (state.Playhead is { } playhead && Math.Abs(TimelineRulerLayout.GetX(state, area, playhead) - position.X) <= PlayheadHalfWidth)
        {
            return false;
        }

        return GetMarkAt(source, state, area, position) is { } marks
               && source.ClickRulerMark(marks.Type,
                                        TimelineRulerLayout.GetValue(state, area, position.X - MarkHalfPixel),
                                        TimelineRulerLayout.GetValue(state, area, position.X + MarkHalfPixel),
                                        TimelineRulerLayout.GetLength(state, area, marks.MinimumWidth));
    }

    private TimelineRulerMarks? GetMarkAt(ITimelineRulerSource source, TimelineRulerState state, TimelineRulerArea area, Point position)
    {
        var lanes = source.GetRulerMarks();

        if (lanes.Count == 0 || position.Y < RulerHeight)
        {
            return null;
        }

        var lane = Math.Clamp((int)((position.Y - RulerHeight) / (StripHeight / lanes.Count)), 0, lanes.Count - 1);

        var marks = lanes[lane];

        var from = TimelineRulerLayout.GetValue(state, area, position.X - MarkHalfPixel);

        var to = TimelineRulerLayout.GetValue(state, area, position.X + MarkHalfPixel);

        return TimelineRulerLayout.IsOverMark(marks, from, to, TimelineRulerLayout.GetLength(state, area, marks.MinimumWidth))
            ? marks
            : null;
    }

    private void UpdateHoverText(Point position)
    {
        var text = Source is { } source && source.GetRulerState() is { } state
            ? GetMarkAt(source, state, GetArea(source, state), position)?.Name
            : null;

        if (text == HoverText)
        {
            return;
        }

        HoverText = text;

        ToolTipService.SetToolTip(this, text);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Drag == RulerDrag.None)
        {
            UpdateHoverText(e.GetCurrentPoint(this).Position);

            return;
        }

        if (Source is not { } source || source.GetRulerState() is not { } state)
        {
            return;
        }

        var area = GetArea(source, state);

        var x = e.GetCurrentPoint(this).Position.X;

        var value = TimelineRulerLayout.GetValue(state, area, x);

        switch (Drag)
        {
            case RulerDrag.Playhead:
                source.MoveRulerPlayhead(TimelineRulerLayout.GetValue(state, area, Math.Clamp(x, area.Left, area.Right)));

                UpdateEdgeScroll(x);

                break;

            case RulerDrag.Start:
                source.MoveRulerCrop(TimelineRulerHandle.Start, value);

                break;

            case RulerDrag.End:
                source.MoveRulerCrop(TimelineRulerHandle.End, value);

                break;
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (Drag == RulerDrag.None)
        {
            return;
        }

        EndDrag();

        ReleasePointerCapture(e.Pointer);

        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndDrag();

    private void EndDrag()
    {
        EdgeTimer.Stop();

        if (Drag is RulerDrag.Start or RulerDrag.End)
        {
            Source?.EndRulerCrop();
        }

        Drag = RulerDrag.None;
    }

    private void UpdateEdgeScroll(double x)
    {
        EdgePointerX = x;

        if (Source is { } source
            && source.GetRulerState() is { } state
            && TimelineRulerLayout.GetEdgeStep(state, GetArea(source, state), x, EdgeZone, EdgeScrollPixels) != 0)
        {
            if (!EdgeTimer.IsRunning)
            {
                EdgeTimer.Start();
            }

            return;
        }

        EdgeTimer.Stop();
    }

    private void OnEdgeTick(DispatcherQueueTimer sender, object args)
    {
        if (Drag != RulerDrag.Playhead || Source is not { } source || source.GetRulerState() is not { Playhead: { } playhead } state)
        {
            EdgeTimer.Stop();

            return;
        }

        var step = TimelineRulerLayout.GetEdgeStep(state, GetArea(source, state), EdgePointerX, EdgeZone, EdgeScrollPixels);

        if (step == 0)
        {
            EdgeTimer.Stop();

            return;
        }

        source.MoveRulerPlayhead(playhead + step);

        if (source.GetRulerState()?.Playhead == playhead)
        {
            EdgeTimer.Stop();
        }
    }

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var flyout = new MenuFlyout();

        flyout.Items.Add(MenuItems.Create(GetPinText(IsPinned), () => IsPinned = !IsPinned));

        if (e.TryGetPosition(this, out var position))
        {
            flyout.ShowAt(this, new FlyoutShowOptions { Position = position });
        }
        else
        {
            flyout.ShowAt(this);
        }

        e.Handled = true;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var dpi = (float)Canvas.Dpi;

        canvas.Scale(dpi);

        var width = e.Info.Width / dpi;

        if (Source is not { } source || source.GetRulerState() is not { } state)
        {
            return;
        }

        var area = GetArea(source, state);

        if (area.Width <= 0)
        {
            return;
        }

        DrawCrop(canvas, state, area);

        DrawTicks(canvas, source, state, area, width);

        canvas.DrawLine(0, RulerHeight, width, RulerHeight, Paints.Tick);

        DrawMarks(canvas, source, state, area);

        DrawHandles(canvas, state, area);

        if (state.Playhead is { } playhead)
        {
            DrawPlayhead(canvas, source, (float)TimelineRulerLayout.GetX(state, area, playhead), playhead, width);
        }
    }

    private void DrawCrop(SKCanvas canvas, TimelineRulerState state, TimelineRulerArea area)
    {
        if (state is not { CropStart: { } start, CropEnd: { } end })
        {
            return;
        }

        Paints.Fill.Color = Paints.Dim;

        var startX = (float)TimelineRulerLayout.GetX(state, area, start);

        var endX = (float)TimelineRulerLayout.GetX(state, area, end);

        if (startX > area.Left)
        {
            canvas.DrawRect(new SKRect((float)area.Left, 0, startX, RulerHeight + StripHeight), Paints.Fill);
        }

        if (endX < area.Right)
        {
            canvas.DrawRect(new SKRect(endX, 0, (float)area.Right, RulerHeight + StripHeight), Paints.Fill);
        }
    }

    private void DrawTicks(SKCanvas canvas, ITimelineRulerSource source, TimelineRulerState state, TimelineRulerArea area, float width)
    {
        var (step, values) = TimelineRulerLayout.GetTicks(state, area, TickSpacing);

        var baseline = GetBaseline(0, RulerHeight - TickLength);

        foreach (var value in values)
        {
            var x = (float)TimelineRulerLayout.GetX(state, area, value);

            canvas.DrawLine(x, RulerHeight - TickLength, x, RulerHeight, Paints.Tick);

            var label = source.FormatRulerTick(value - state.Origin, step);

            if (x + LabelGap + Paints.Font.MeasureText(label) < width)
            {
                canvas.DrawText(label, x + LabelGap, baseline, SKTextAlign.Left, Paints.Font, Paints.Label);
            }
        }
    }

    private void DrawMarks(SKCanvas canvas, ITimelineRulerSource source, TimelineRulerState state, TimelineRulerArea area)
    {
        var lanes = source.GetRulerMarks();

        if (lanes.Count == 0)
        {
            return;
        }

        var laneHeight = StripHeight / lanes.Count;

        for (var lane = 0; lane < lanes.Count; lane++)
        {
            DrawLane(canvas, lanes[lane], state, area, RulerHeight + lane * laneHeight, laneHeight);
        }
    }

    private void DrawLane(SKCanvas canvas,
                          TimelineRulerMarks marks,
                          TimelineRulerState state,
                          TimelineRulerArea area,
                          float top,
                          float height)
    {
        float? left = null;

        var right = 0f;

        var colour = SKColors.Transparent;

        for (var index = SortedSearch.FirstAtOrAfter(marks.Ends, state.ViewStart);
             index < marks.Starts.Length && marks.Starts[index] <= state.ViewEnd;
             index++)
        {
            var start = (float)TimelineRulerLayout.GetX(state, area, marks.Starts[index]);

            var end = Math.Max((float)TimelineRulerLayout.GetX(state, area, marks.Ends[index]), start + marks.MinimumWidth);

            var markColour = marks.GetColour(index);

            if (left is not null && start <= right && markColour == colour)
            {
                right = Math.Max(right, end);

                continue;
            }

            if (left is { } drawn)
            {
                DrawMark(canvas, drawn, right, top, height, colour);
            }

            left = start;

            right = end;

            colour = markColour;
        }

        if (left is { } last)
        {
            DrawMark(canvas, last, right, top, height, colour);
        }
    }

    private void DrawMark(SKCanvas canvas, float left, float right, float top, float height, SKColor colour)
    {
        Paints.Fill.Color = colour;

        canvas.DrawRect(left, top, right - left, height, Paints.Fill);
    }

    private void DrawHandles(SKCanvas canvas, TimelineRulerState state, TimelineRulerArea area)
    {
        var (startX, endX) = TimelineRulerLayout.GetHandleXs(state, area, HandleGap);

        Paints.Fill.Color = HandleColour;

        DrawHandle(canvas, StartHandle, startX);

        DrawHandle(canvas, EndHandle, endX);
    }

    private void DrawHandle(SKCanvas canvas, SKPath handle, double? x)
    {
        if (x is not { } left)
        {
            return;
        }

        canvas.Save();

        canvas.Translate((float)left, RulerHeight + StripHeight - HandleHeight);

        canvas.DrawPath(handle, Paints.Fill);

        canvas.Restore();
    }

    private void DrawPlayhead(SKCanvas canvas, ITimelineRulerSource source, float x, double value, float width)
    {
        canvas.Save();

        canvas.Translate(x, 0);

        canvas.DrawPath(Triangle, Paints.PlayheadFill);

        canvas.Restore();

        var text = source.FormatRulerPlayhead(value);

        var badgeWidth = Paints.Font.MeasureText(text) + PlayheadBadgePadding * 2;

        var badgeHeight = RulerHeight - 2;

        var left = Math.Clamp(x - badgeWidth / 2, 0, Math.Max(0, width - badgeWidth));

        canvas.DrawRoundRect(new SKRect(left, 0, left + badgeWidth, badgeHeight), BadgeRadius, BadgeRadius, Paints.PlayheadFill);

        canvas.DrawText(text, left + PlayheadBadgePadding, GetBaseline(0, badgeHeight), SKTextAlign.Left, Paints.Font, Paints.PlayheadText);
    }

    private float GetBaseline(float top, float height)
    {
        var metrics = Paints.Font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ruler = (TimelineRuler)d;

        if (e.OldValue is ITimelineRulerSource previous)
        {
            previous.RulerChanged -= ruler.OnSourceRulerChanged;
        }

        if (e.NewValue is ITimelineRulerSource source)
        {
            source.RulerChanged += ruler.OnSourceRulerChanged;
        }

        ruler.Drag = RulerDrag.None;

        ruler.Canvas.Invalidate();
    }

    private enum RulerDrag
    {
        None,
        Playhead,
        Start,
        End
    }
}
