using InternalsViewer.Replay.Events;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed class EventTimelineControl : Grid
{
    private const double HandleWidth = 6;
    private const double CropHandleHitArea = 10;

    private readonly Canvas _canvas;
    private readonly Line _playhead;
    private readonly Rectangle _cropOverlay;
    private readonly Rectangle _cropHandleLeft;
    private readonly Rectangle _cropHandleRight;

    private bool _isDragging;
    private bool _isCropDragging;
    private CropDragTarget _cropDragTarget;

    private double _cropStartX = -1;
    private double _cropEndX = -1;

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
        var control = (EventTimelineControl)d;
        var events = (List<EngineEvent>)e.NewValue;

        if (events.Count > 0)
        {
            var maxTimeMs = events.Max(m => m.TimeMs);
            control.PixelsPerMs = control.ActualWidth / maxTimeMs;
        }

        control._cropStartX = -1;
        control._cropEndX = -1;
        control.CurrentSequenceFrom = 0;
        control.CurrentSequenceTo = 0;

        control.Render();
    }

    public double PixelsPerMs { get; set; } = 0.2;

    public double CurrentTimeMs { get; private set; }

    public long CurrentSequenceFrom { get; private set; }

    public long CurrentSequenceTo { get; private set; }

    public event Action<long, long>? SequenceChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Black);

        _canvas = new Canvas();
        Children.Add(_canvas);

        _cropOverlay = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            IsHitTestVisible = false
        };

        _cropHandleLeft = new Rectangle
        {
            Width = HandleWidth,
            Fill = new SolidColorBrush(Colors.White),
            Opacity = 0.8,
            IsHitTestVisible = false
        };

        _cropHandleRight = new Rectangle
        {
            Width = HandleWidth,
            Fill = new SolidColorBrush(Colors.White),
            Opacity = 0.8,
            IsHitTestVisible = false
        };

        _playhead = new Line
        {
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2
        };

        _canvas.Children.Add(_cropOverlay);
        _canvas.Children.Add(_cropHandleLeft);
        _canvas.Children.Add(_cropHandleRight);
        _canvas.Children.Add(_playhead);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;

        SizeChanged += (_, _) =>
        {
            _canvas.Width = ActualWidth;
            _canvas.Height = ActualHeight;
            if (Events?.Count > 0)
            {
                PixelsPerMs = ActualWidth / Events.Max(m => m.TimeMs);
            }

            UpdateOverlays();
        };
    }

    public void Render()
    {
        _canvas.Children.Clear();

        _canvas.Children.Add(_cropOverlay);
        _canvas.Children.Add(_cropHandleLeft);
        _canvas.Children.Add(_cropHandleRight);

        foreach (var e in Events.OrderBy(e => e.TimeMs))
        {
            double x = e.TimeMs * PixelsPerMs;

            var marker = new Rectangle
            {
                Width = 3,
                Height = ActualHeight,
                Fill = new SolidColorBrush(GetColor(e))
            };

            Canvas.SetLeft(marker, x);
            Canvas.SetTop(marker, 0);

            _canvas.Children.Add(marker);
        }

        _canvas.Children.Add(_playhead);

        UpdateOverlays();
    }

    private Color GetColor(EngineEvent e) => e switch
    {
        IoEvent   
            => Colors.CornflowerBlue,
        LockEvent 
            => Colors.Red,
        WaitEvent 
            => Colors.Orange,
        PageEvent 
            => Colors.LimeGreen,
        _         
            => Colors.Gray
    };

    private void UpdatePlayhead()
    {
        double x = CurrentTimeMs * PixelsPerMs;

        _playhead.X1 = x;
        _playhead.X2 = x;
        _playhead.Y1 = 0;
        _playhead.Y2 = ActualHeight;
    }

    private bool HasCrop => _cropStartX >= 0 && _cropEndX >= 0;

    private void UpdateOverlays()
    {
        UpdatePlayhead();

        if (!HasCrop)
        {
            _cropOverlay.Visibility = Visibility.Collapsed;
            _cropHandleLeft.Visibility = Visibility.Collapsed;
            _cropHandleRight.Visibility = Visibility.Collapsed;
            return;
        }

        var left  = Math.Min(_cropStartX, _cropEndX);
        var right = Math.Max(_cropStartX, _cropEndX);
        var h = ActualHeight;

        _cropOverlay.Width  = Math.Max(0, right - left);
        _cropOverlay.Height = h;
        Canvas.SetLeft(_cropOverlay, left);
        Canvas.SetTop(_cropOverlay, 0);
        _cropOverlay.Visibility = Visibility.Visible;

        _cropHandleLeft.Height = h;
        Canvas.SetLeft(_cropHandleLeft, left - HandleWidth / 2);
        Canvas.SetTop(_cropHandleLeft, 0);
        _cropHandleLeft.Visibility = Visibility.Visible;

        _cropHandleRight.Height = h;
        Canvas.SetLeft(_cropHandleRight, right - HandleWidth / 2);
        Canvas.SetTop(_cropHandleRight, 0);
        _cropHandleRight.Visibility = Visibility.Visible;
    }

    private long XToSequenceId(double x)
    {
        if (Events.Count == 0)
        {
            return 0;
        }

        var timeMs = x / PixelsPerMs;

        var nearest = Events.MinBy(e => Math.Abs(e.TimeMs - timeMs));

        return nearest?.SequenceId ?? 0;
    }

    private double SequenceIdToX(long sequenceId)
    {
        if (Events.Count == 0)
        {
            return 0;
        }

        var e = Events.FirstOrDefault(ev => ev.SequenceId == sequenceId);
        return e != null ? e.TimeMs * PixelsPerMs : 0;
    }

    private void FireSequenceChanged(double fromX, double toX)
    {
        var left  = Math.Min(fromX, toX);
        var right = Math.Max(fromX, toX);

        CurrentSequenceFrom = XToSequenceId(left);
        CurrentSequenceTo   = XToSequenceId(right);

        SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
    }

    private CropDragTarget HitTest(double x)
    {
        if (!HasCrop)
        {
            return CropDragTarget.NewCrop;
        }

        var left  = Math.Min(_cropStartX, _cropEndX);
        var right = Math.Max(_cropStartX, _cropEndX);

        if (Math.Abs(x - left)  <= CropHandleHitArea)
        {
            return CropDragTarget.Left;
        }

        if (Math.Abs(x - right) <= CropHandleHitArea)
        {
            return CropDragTarget.Right;
        }

        if (x > left && x < right)
        {
            return CropDragTarget.Body;
        }

        return CropDragTarget.NewCrop;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        CapturePointer(e.Pointer);

        var position = e.GetCurrentPoint(_canvas).Position;

        _cropDragTarget = HitTest(position.X);

        _isDragging = true;
        _isCropDragging = false;

        if (_cropDragTarget == CropDragTarget.NewCrop)
        {
            _cropStartX = position.X;
            _cropEndX = position.X;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var pos = e.GetCurrentPoint(_canvas).Position;
        var x = Math.Clamp(pos.X, 0, ActualWidth);

        _isCropDragging = true;

        switch (_cropDragTarget)
        {
            case CropDragTarget.NewCrop:
                _cropEndX = x;
                
                UpdateOverlays();

                if (Math.Abs(_cropEndX - _cropStartX) > 2)
                {
                    FireSequenceChanged(_cropStartX, _cropEndX);
                }

                break;

            case CropDragTarget.Left:
                _cropStartX = x;
                UpdateOverlays();
                FireSequenceChanged(_cropStartX, _cropEndX);
                break;

            case CropDragTarget.Right:
                _cropEndX = x;
                UpdateOverlays();
                FireSequenceChanged(_cropStartX, _cropEndX);
                break;

            case CropDragTarget.Body:
                var width = Math.Abs(_cropEndX - _cropStartX);
                var newLeft = Math.Clamp(x - width / 2, 0, ActualWidth - width);
                _cropStartX = newLeft;
                _cropEndX = newLeft + width;
                UpdateOverlays();
                FireSequenceChanged(_cropStartX, _cropEndX);
                break;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCaptures();

        if (!_isDragging)
        {
            return;
        }

        var pos = e.GetCurrentPoint(_canvas).Position;
        var x   = Math.Clamp(pos.X, 0, ActualWidth);

        if (!_isCropDragging)
        {
            // Plain click with no drag: scrub to this position (clear any crop)
            _cropStartX = -1;
            _cropEndX = -1;

            CurrentSequenceFrom = 0;
            CurrentSequenceTo = XToSequenceId(x);

            CurrentTimeMs = x / PixelsPerMs;

            UpdateOverlays();

            SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
        }
        else if (_cropDragTarget == CropDragTarget.NewCrop && Math.Abs(_cropEndX - _cropStartX) <= 2)
        {
            // Tiny drag treated as a click
            _cropStartX = -1;
            _cropEndX   = -1;

            CurrentSequenceFrom = 0;
            CurrentSequenceTo   = XToSequenceId(x);

            CurrentTimeMs = x / PixelsPerMs;

            UpdateOverlays();
            SequenceChanged?.Invoke(CurrentSequenceFrom, CurrentSequenceTo);
        }

        _isDragging = false;
        _isCropDragging = false;
    }
}
