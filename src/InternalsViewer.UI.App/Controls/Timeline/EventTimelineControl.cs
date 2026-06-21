using InternalsViewer.Replay.Events;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed class EventTimelineControl : Grid
{
    private const float HandleWidth = 6f;
    private const double CropHandleHitArea = 10;
    private const double MaxResolutionMs = 10.0;
    private const float RowLabelWidth = 36f;
    private const float RowPadding = 2f;
    private const float MarkerWidth = 2f;

    private static readonly TimeSpan PlayInterval = TimeSpan.FromMilliseconds(80);

    private static readonly (Type EventType, string Label, SKColor Color)[] Rows =
    [
        (typeof(IoEvent),   "I/O",   ColourConstants.IoColour.ToSkColor().WithAlpha(255)),
        (typeof(PageEvent), "Page", ColourConstants.PageColour.ToSkColor().WithAlpha(255)),
        (typeof(LockEvent), "Lock", ColourConstants.LockColour.ToSkColor().WithAlpha(255)),
        (typeof(WaitEvent), "Wait", ColourConstants.WaitColour.ToSkColor().WithAlpha(255)),
    ];

    private readonly Button _playButton;
    private readonly SKXamlCanvas _skCanvas;
    private readonly Canvas _overlay;

    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);

    private readonly SKPaint _labelPaint = new()
    {
        Color = SKColors.LightGray,
        IsAntialias = true,
    };

    private readonly SKPaint _rowBgPaint = new() { Style = SKPaintStyle.Fill };
    private readonly SKPaint _markerPaint = new() { Style = SKPaintStyle.Fill };
    private readonly SKPaint _playheadPaint = new()
    {
        Color = SKColors.IndianRed,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };
    private readonly SKPaint _cropOverlayPaint = new()
    {
        Color = new SKColor(255, 255, 255, 55),
        Style = SKPaintStyle.Fill,
    };
    private readonly SKPaint _cropHandlePaint = new()
    {
        Color = new SKColor(255, 255, 255, 200),
        Style = SKPaintStyle.Fill,
    };

    private List<EngineEvent> _sortedEvents = [];
    private List<double> _effectiveTimes = [];

    private double _minTime;
    private double _maxTime;
    private double _timeRange;

    private int _playIndex;
    private int _displayIndex;
    private bool _isPlaying;
    private readonly DispatcherTimer _playTimer;

    private bool _isDragging;
    private bool _isCropDragging;
    private CropDragTarget _cropDragTarget;

    private double _cropStartTime = -1;
    private double _cropEndTime = -1;

    private enum CropDragTarget { None, Left, Right, Body, NewCrop }

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventTimelineControl),
            new PropertyMetadata(new List<EngineEvent>(), OnEventsChanged));

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (EventTimelineControl)d;
        var events = (List<EngineEvent>)e.NewValue;

        ctrl._sortedEvents = [.. events.OrderBy(ev => ev.SequenceId)];
        ctrl.BuildEffectiveTimes();

        ctrl.StopPlay();
        ctrl._cropStartTime = -1;
        ctrl._cropEndTime = -1;
        ctrl.CurrentSequenceFrom = 0;
        ctrl.CurrentSequenceTo = 0;
        ctrl._playIndex = 0;
        ctrl._displayIndex = 0;

        ctrl._skCanvas.Invalidate();
    }

    public long CurrentSequenceFrom { get; private set; }
    public long CurrentSequenceTo { get; private set; }

    public event Action<long, long>? SequenceChanged;

    /// <summary>Raised when auto-play starts (true) or stops (false).</summary>
    public event Action<bool>? PlayStateChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Transparent);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _playButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE768", FontSize = 14 },
            Width = 40,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(0, 30, 30, 30)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
        };
        _playButton.Click += OnPlayButtonClick;
        Grid.SetColumn(_playButton, 0);
        Children.Add(_playButton);

        _skCanvas = new SKXamlCanvas { IgnorePixelScaling = true };
        _skCanvas.PaintSurface += OnPaintSurface;
        Grid.SetColumn(_skCanvas, 1);
        Children.Add(_skCanvas);

        _overlay = new Canvas { Background = new SolidColorBrush(Colors.Transparent) };
        Grid.SetColumn(_overlay, 1);
        Children.Add(_overlay);

        _overlay.PointerPressed += OnPointerPressed;
        _overlay.PointerMoved += OnPointerMoved;
        _overlay.PointerReleased += OnPointerReleased;
        _overlay.SizeChanged += (_, _) => _skCanvas.Invalidate();

        _playTimer = new DispatcherTimer { Interval = PlayInterval };
        _playTimer.Tick += OnPlayTimerTick;
    }

    private void BuildEffectiveTimes()
    {
        _effectiveTimes = new List<double>(_sortedEvents.Count);

        if (_sortedEvents.Count == 0)
        {
            _minTime = 0;
            _maxTime = 1;
            _timeRange = 1;
            return;
        }

        var bucketCounts = new Dictionary<long, int>();
        foreach (var ev in _sortedEvents)
        {
            var bucket = (long)Math.Floor(ev.TimeMs / MaxResolutionMs);
            bucketCounts[bucket] = bucketCounts.TryGetValue(bucket, out var c) ? c + 1 : 1;
        }

        var bucketIndex = new Dictionary<long, int>();
        foreach (var ev in _sortedEvents)
        {
            var bucket = (long)Math.Floor(ev.TimeMs / MaxResolutionMs);
            var bucketStart = bucket * MaxResolutionMs;
            bucketIndex.TryGetValue(bucket, out var idx);
            var count = bucketCounts[bucket];
            var step = count > 1 ? MaxResolutionMs / count : 0.0;
            _effectiveTimes.Add(bucketStart + idx * step);
            bucketIndex[bucket] = idx + 1;
        }

        _minTime = _effectiveTimes.Min();
        _maxTime = _effectiveTimes.Max();
        _timeRange = Math.Max(_maxTime - _minTime, 1.0);
    }

    private float CanvasWidth => (float)_overlay.ActualWidth;
    private float CanvasHeight => (float)_overlay.ActualHeight;
    private float DrawWidth => CanvasWidth - RowLabelWidth;

    private float TimeToX(double effectiveTimeMs)
        => RowLabelWidth + (float)((effectiveTimeMs - _minTime) / _timeRange * DrawWidth);

    private double XToTime(double x)
        => _minTime + Math.Max(0, x - RowLabelWidth) / DrawWidth * _timeRange;

    private int XToEventIndex(double x)
    {
        if (_sortedEvents.Count == 0) return 0;
        var t = XToTime(x);
        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < _effectiveTimes.Count; i++)
        {
            var d = Math.Abs(_effectiveTimes[i] - t);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private float RowY(int rowIndex, float rowHeight) => rowIndex * rowHeight + RowPadding;
    private float RowH(float rowHeight) => rowHeight - RowPadding * 2;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var w = e.Info.Width;
        var h = e.Info.Height;

        if (_sortedEvents.Count == 0 || w <= 0 || h <= 0) return;

        var rowCount = Rows.Length;
        var rowHeight = (float)h / rowCount;

        for (var r = 0; r < rowCount; r++)
        {
            var (_, label, _) = Rows[r];
            var y = r * rowHeight;

            _rowBgPaint.Color = r % 2 == 0
                ? new SKColor(30, 30, 30, 220)
                : new SKColor(20, 20, 20, 220);
            canvas.DrawRect(0, y, w, rowHeight, _rowBgPaint);

            canvas.DrawText(label, 2, y + rowHeight / 2 + _labelFont.Size / 2, SKTextAlign.Left, _labelFont, _labelPaint);

            using var sepPaint = new SKPaint { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };
            canvas.DrawLine(0, y + rowHeight, w, y + rowHeight, sepPaint);
        }

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var ev = _sortedEvents[i];
            var x = TimeToX(_effectiveTimes[i]);
            var rowIndex = GetRowIndex(ev);
            if (rowIndex < 0) continue;

            _markerPaint.Color = Rows[rowIndex].Color;
            canvas.DrawRect(x, RowY(rowIndex, rowHeight), MarkerWidth, RowH(rowHeight), _markerPaint);
        }

        if (HasCrop)
        {
            var leftX = (float)TimeToX(Math.Min(_cropStartTime, _cropEndTime));
            var rightX = (float)TimeToX(Math.Max(_cropStartTime, _cropEndTime));

            canvas.DrawRect(leftX, 0, rightX - leftX, h, _cropOverlayPaint);
            canvas.DrawRect(leftX - HandleWidth / 2, 0, HandleWidth, h, _cropHandlePaint);
            canvas.DrawRect(rightX - HandleWidth / 2, 0, HandleWidth, h, _cropHandlePaint);
        }

        if (_displayIndex < _effectiveTimes.Count)
        {
            var px = TimeToX(_effectiveTimes[_displayIndex]);
            canvas.DrawLine(px, 0, px, h, _playheadPaint);
        }
    }

    private static int GetRowIndex(EngineEvent ev)
    {
        for (var i = 0; i < Rows.Length; i++)
            if (Rows[i].EventType.IsInstanceOfType(ev)) return i;
        return -1;
    }

    private bool HasCrop => _cropStartTime >= 0 && _cropEndTime >= 0
                         && Math.Abs(_cropEndTime - _cropStartTime) > 0.0001;

    private void FireSequenceChanged(double fromTime, double toTime)
    {
        var left = Math.Min(fromTime, toTime);
        var right = Math.Max(fromTime, toTime);

        CurrentSequenceFrom = SequenceIdAtOrAfter(left);
        CurrentSequenceTo = SequenceIdAtOrBefore(right);

        if (CurrentSequenceFrom > CurrentSequenceTo)
        {
            var nearest = NearestSequenceId((left + right) / 2);
            CurrentSequenceFrom = nearest;
            CurrentSequenceTo = nearest;
        }

        SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
    }

    private long NearestSequenceId(double effectiveTimeMs)
    {
        if (_sortedEvents.Count == 0) return 0;
        var best = 0;
        var bestDist = double.MaxValue;
        for (var i = 0; i < _effectiveTimes.Count; i++)
        {
            var d = Math.Abs(_effectiveTimes[i] - effectiveTimeMs);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return _sortedEvents[best].SequenceId;
    }

    private long SequenceIdAtOrAfter(double t)
    {
        if (_sortedEvents.Count == 0) return 0;
        for (var i = 0; i < _effectiveTimes.Count; i++)
            if (_effectiveTimes[i] >= t) return _sortedEvents[i].SequenceId;
        return _sortedEvents[^1].SequenceId;
    }

    private long SequenceIdAtOrBefore(double t)
    {
        if (_sortedEvents.Count == 0) return 0;
        for (var i = _effectiveTimes.Count - 1; i >= 0; i--)
            if (_effectiveTimes[i] <= t) return _sortedEvents[i].SequenceId;
        return _sortedEvents[0].SequenceId;
    }

    private CropDragTarget HitTest(double x)
    {
        if (!HasCrop) return CropDragTarget.NewCrop;

        var leftX = TimeToX(Math.Min(_cropStartTime, _cropEndTime));
        var rightX = TimeToX(Math.Max(_cropStartTime, _cropEndTime));

        if (Math.Abs(x - leftX) <= CropHandleHitArea) return CropDragTarget.Left;
        if (Math.Abs(x - rightX) <= CropHandleHitArea) return CropDragTarget.Right;
        if (x > leftX && x < rightX) return CropDragTarget.Body;

        return CropDragTarget.NewCrop;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _overlay.CapturePointer(e.Pointer);
        var x = e.GetCurrentPoint(_overlay).Position.X;

        _cropDragTarget = HitTest(x);
        _isDragging = true;
        _isCropDragging = false;

        if (_cropDragTarget == CropDragTarget.NewCrop)
        {
            var t = XToTime(x);
            _cropStartTime = t;
            _cropEndTime = t;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;

        var x = Math.Clamp(e.GetCurrentPoint(_overlay).Position.X, RowLabelWidth, CanvasWidth);
        var t = XToTime(x);

        _isCropDragging = true;

        switch (_cropDragTarget)
        {
            case CropDragTarget.NewCrop:
                _cropEndTime = t;
                if (HasCrop) FireSequenceChanged(_cropStartTime, _cropEndTime);
                break;

            case CropDragTarget.Left:
                _cropStartTime = t;
                FireSequenceChanged(_cropStartTime, _cropEndTime);
                break;

            case CropDragTarget.Right:
                _cropEndTime = t;
                FireSequenceChanged(_cropStartTime, _cropEndTime);
                break;

            case CropDragTarget.Body:
                var span = _cropEndTime - _cropStartTime;
                var newLeft = Math.Clamp(t - span / 2, _minTime, _maxTime - span);
                _cropStartTime = newLeft;
                _cropEndTime = newLeft + span;
                FireSequenceChanged(_cropStartTime, _cropEndTime);
                break;
        }

        _skCanvas.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _overlay.ReleasePointerCaptures();
        if (!_isDragging) return;

        var x = Math.Clamp(e.GetCurrentPoint(_overlay).Position.X, RowLabelWidth, CanvasWidth);

        if (!_isCropDragging || (_cropDragTarget == CropDragTarget.NewCrop && !HasCrop))
        {
            _cropStartTime = -1;
            _cropEndTime = -1;

            _displayIndex = XToEventIndex(x);
            _playIndex = _displayIndex;
            CurrentSequenceFrom = 0;
            CurrentSequenceTo = _sortedEvents.Count > 0 ? _sortedEvents[_displayIndex].SequenceId : 0;

            _skCanvas.Invalidate();
            SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
        }

        _isDragging = false;
        _isCropDragging = false;
    }

    private void OnPlayButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isPlaying) StopPlay();
        else StartPlay();
    }

    private void StartPlay()
    {
        if (_sortedEvents.Count == 0) return;

        _playIndex = 0;
        _displayIndex = 0;
        _isPlaying = true;

        _cropStartTime = -1;
        _cropEndTime = -1;
        CurrentSequenceFrom = 0;
        CurrentSequenceTo = 0;

        SetPlayButtonIcon(isPlaying: true);
        _playTimer.Start();

        PlayStateChanged?.Invoke(true);
    }

    private void StopPlay()
    {
        var wasPlaying = _isPlaying;

        _playTimer.Stop();
        _isPlaying = false;
        SetPlayButtonIcon(isPlaying: false);

        if (wasPlaying)
        {
            PlayStateChanged?.Invoke(false);
        }
    }

    private void SetPlayButtonIcon(bool isPlaying)
    {
        if (_playButton.Content is FontIcon icon)
            icon.Glyph = isPlaying ? "\uE769" : "\uE768";
    }

    private void OnPlayTimerTick(object? sender, object e)
    {
        if (_playIndex >= _sortedEvents.Count)
        {
            StopPlay();
            return;
        }

        _displayIndex = _playIndex;
        CurrentSequenceFrom = 0;
        CurrentSequenceTo = _sortedEvents[_displayIndex].SequenceId;

        _skCanvas.Invalidate();
        SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);

        _playIndex++;
    }
}
