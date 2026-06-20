using InternalsViewer.Replay.Events;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed class EventTimelineControl : Grid
{
    private readonly Canvas _canvas;

    private readonly Line _playhead;

    private bool _isDragging;

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

            var pixelsPerMs = control.ActualWidth / maxTimeMs;

            control.PixelsPerMs = pixelsPerMs;
        }

        control.Render();
    }

    public double PixelsPerMs { get; set; } = 0.2;

    public double CurrentTimeMs { get; private set; }

    public event Action<double>? TimeChanged;

    public EventTimelineControl()
    {
        Background = new SolidColorBrush(Colors.Black);

        _canvas = new Canvas();
        Children.Add(_canvas);

        _playhead = new Line
        {
            Stroke = new SolidColorBrush(Colors.Yellow),
            StrokeThickness = 2
        };

        _canvas.Children.Add(_playhead);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += (_, _) => _isDragging = false;

        SizeChanged += (_, _) => UpdatePlayhead();
    }

    public void Render()
    {
        _canvas.Children.Clear();

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
            Canvas.SetTop(marker,0);

            _canvas.Children.Add(marker);
        }

        _canvas.Children.Add(_playhead);

        UpdatePlayhead();
    }

    private Color GetColor(EngineEvent e)
    {
        return e switch
        {
            IoEvent => Colors.CornflowerBlue,
            LockEvent => Colors.Red,
            WaitEvent => Colors.Orange,
            PageEvent => Colors.LimeGreen,
            _ => Colors.Gray
        };
    }

    private void UpdatePlayhead()
    {
        double x = CurrentTimeMs * PixelsPerMs;

        _playhead.X1 = x;
        _playhead.X2 = x;
        _playhead.Y1 = 0;
        _playhead.Y2 = ActualHeight;
    }

    private void SetTimeFromPosition(double x)
    {
        CurrentTimeMs = x / PixelsPerMs;

        UpdatePlayhead();
        TimeChanged?.Invoke(CurrentTimeMs);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = true;

        var pos = e.GetCurrentPoint(_canvas).Position;

        SetTimeFromPosition(pos.X);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = e.GetCurrentPoint(_canvas).Position;

        SetTimeFromPosition(position.X);
    }


    private async Task PlayAsync(EventTimelineControl timeline)
    {
        double duration = timeline.Events.Max(e => e.TimeMs);

        timeline.CurrentTimeMs = 0;

        while (timeline.CurrentTimeMs < duration)
        {
            timeline.CurrentTimeMs += 5;

            timeline.Render();
            await Task.Delay(16);
        }
    }

}