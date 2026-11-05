using System;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Timeline;

public sealed class PinnedTransport : UserControl, IDisposable
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source),
                                    typeof(ITimelineTransport),
                                    typeof(PinnedTransport),
                                    new PropertyMetadata(null, OnSourceChanged));

    public ITimelineTransport? Source
    {
        get => (ITimelineTransport?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public PinnedTransport()
    {
        Transport = new TimelineTransport(isPlaybackOnly: true);

        Transport.PlayPauseRequested += OnPlayPauseRequested;

        Transport.StepRequested += OnStepRequested;

        Transport.PlaySpeedChanged += OnPlaySpeedChanged;

        Content = Transport;
    }

    private TimelineTransport Transport { get; }

    public void Dispose()
    {
        Source = null;

        Transport.PlayPauseRequested -= OnPlayPauseRequested;

        Transport.StepRequested -= OnStepRequested;

        Transport.PlaySpeedChanged -= OnPlaySpeedChanged;

        Transport.Dispose();
    }

    private void OnPlayPauseRequested() => Source?.TogglePlay();

    private void OnStepRequested(bool forward) => Source?.Step(forward);

    private void OnPlaySpeedChanged(double speed)
    {
        if (Source is { } source)
        {
            source.PlaySpeed = speed;
        }
    }

    private void OnSourcePlayStateChanged(bool isPlaying) => Transport.SetPlaying(isPlaying);

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var transport = (PinnedTransport)d;

        if (e.OldValue is ITimelineTransport previous)
        {
            previous.PlayStateChanged -= transport.OnSourcePlayStateChanged;
        }

        if (e.NewValue is ITimelineTransport source)
        {
            source.PlayStateChanged += transport.OnSourcePlayStateChanged;

            transport.Transport.SetPlaying(source.IsPlaying);

            transport.Transport.SetSpeed(source.PlaySpeed);
        }
        else
        {
            transport.Transport.SetPlaying(false);
        }
    }
}
