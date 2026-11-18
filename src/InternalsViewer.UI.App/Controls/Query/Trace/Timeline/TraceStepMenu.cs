using System;
using System.Linq;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed class TraceStepMenu : UserControl, IDisposable
{
    private const string StepGroup = "TraceTimelineStep";

    private const string CallStepText = "Call";

    private const string SameCallStepText = "Same Call";

    private const string TracksStepText = "Tracks";

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source),
                                    typeof(ITimelineRulerSource),
                                    typeof(TraceStepMenu),
                                    new PropertyMetadata(null, OnSourceChanged));

    public ITimelineRulerSource? Source
    {
        get => (ITimelineRulerSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public TraceStepMenu()
    {
        StepLabel = TabCommands.Label(GetStepLabel());

        var menu = new MenuFlyout();

        menu.Opening += (_, _) => FillStepMenu(menu);

        Content = TabCommands.Menu(StepLabel,
                                   "Choose what the step buttons step to. With Tracks or a track, Play steps through their ticks",
                                   menu);

        IsEnabled = false;
    }

    private TraceTimelineControl? Timeline => Source as TraceTimelineControl;

    private TextBlock StepLabel { get; }

    public void Dispose() => Source = null;

    private void FillStepMenu(MenuFlyout menu)
    {
        menu.Items.Clear();

        if (Timeline is not { } timeline)
        {
            return;
        }

        menu.Items.Add(StepItem(timeline, CallStepText, PlayheadStepMode.Call, null));

        menu.Items.Add(StepItem(timeline, SameCallStepText, PlayheadStepMode.SameCall, null));

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(StepItem(timeline, TracksStepText, PlayheadStepMode.Tracks, null));

        var tracks = timeline.GetStepTracks().ToList();

        if (tracks.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var track in tracks)
        {
            menu.Items.Add(StepItem(timeline, track.MenuText, PlayheadStepMode.Track, track.Name));
        }
    }

    private static RadioMenuFlyoutItem StepItem(TraceTimelineControl timeline, string text, PlayheadStepMode mode, string? track)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = StepGroup,
            IsChecked = timeline.StepMode == mode && string.Equals(timeline.StepTrack, track, StringComparison.Ordinal)
        };

        item.Click += (_, _) => timeline.SetStepMode(mode, track);

        return item;
    }

    private string GetStepLabel()
    {
        var text = Timeline is not { } timeline
            ? CallStepText
            : timeline.StepMode switch
            {
                PlayheadStepMode.SameCall => SameCallStepText,
                PlayheadStepMode.Tracks => TracksStepText,
                PlayheadStepMode.Track => timeline.GetStepTrack(timeline.StepTrack)?.MenuText ?? CallStepText,
                _ => CallStepText
            };

        return $"Step {text}";
    }

    private void OnStepModeChanged() => StepLabel.Text = GetStepLabel();

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var stepMenu = (TraceStepMenu)d;

        if (e.OldValue is TraceTimelineControl previous)
        {
            previous.StepModeChanged -= stepMenu.OnStepModeChanged;
        }

        if (e.NewValue is TraceTimelineControl timeline)
        {
            timeline.StepModeChanged += stepMenu.OnStepModeChanged;
        }

        stepMenu.IsEnabled = stepMenu.Timeline is not null;

        stepMenu.OnStepModeChanged();
    }
}
