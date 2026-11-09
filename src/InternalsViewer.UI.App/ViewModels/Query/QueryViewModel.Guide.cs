using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    [ObservableProperty]
    private double _guideStepIndex;

    [ObservableProperty]
    private double _guideStepMaximum;

    [ObservableProperty]
    private IReadOnlyList<GuideStepGroup> _guideStepGroups = [];

    [ObservableProperty]
    private GuideStepItem? _currentGuideStep;

    [ObservableProperty]
    private bool _isGuidePlaying;

    public GuideTransport GuidePlayback => CreatedGuidePlayback ??= CreateGuidePlayback();

    public ITimelineTransport? TransportSource => Layout.IsGuideMode ? GuidePlayback : RulerSource;

    private GuideTransport? CreatedGuidePlayback { get; set; }

    private List<TraceTrackStep>? GuideSteps { get; set; }

    private List<GuideStepItem> GuideStepItems { get; set; } = [];

    private IReadOnlySet<string>? GuideStepsHidden { get; set; }

    private bool IsSelectingGuideStep { get; set; }

    public void SelectGuideStep(GuideStepItem item)
    {
        GuidePlayback.Stop();

        GoToGuideStep(item.Index);
    }

    [RelayCommand]
    private void GuidePlay() => GuidePlayback.TogglePlay();

    [RelayCommand]
    private void GuideNext() => GuidePlayback.Step(forward: true);

    [RelayCommand]
    private void GuidePrevious() => GuidePlayback.Step(forward: false);

    [RelayCommand]
    private void GuideNextPhase() => StepGuideManually(forward: true, GetPhaseGuideIndex);

    [RelayCommand]
    private void GuidePreviousPhase() => StepGuideManually(forward: false, GetPhaseGuideIndex);

    [RelayCommand]
    private void GuideNextOperator() => StepGuideManually(forward: true, GetOperatorGuideIndex);

    [RelayCommand]
    private void GuidePreviousOperator() => StepGuideManually(forward: false, GetOperatorGuideIndex);

    private GuideTransport CreateGuidePlayback()
    {
        var playback = new GuideTransport(StepGuide);

        playback.PlayStateChanged += OnGuidePlayStateChanged;

        return playback;
    }

    private void OnGuidePlayStateChanged(bool isPlaying) => IsGuidePlaying = isPlaying;

    private bool StepGuide(bool forward) => StepGuide(forward, GetNextGuideIndex);

    private void StepGuideManually(bool forward, Func<int, bool, int> getIndex)
    {
        GuidePlayback.Stop();

        StepGuide(forward, getIndex);
    }

    private bool StepGuide(bool forward, Func<int, bool, int> getIndex)
    {
        if (Trace is null)
        {
            return false;
        }

        var index = getIndex(GetCurrentGuideIndex(), forward);

        if (index < 0 || index >= GetGuideSteps().Count)
        {
            return false;
        }

        GoToGuideStep(index);

        return true;
    }

    private int GetCurrentGuideIndex()
    {
        if (TraceTimelineTab.Playhead is not { } position)
        {
            return -1;
        }

        var steps = GetGuideSteps();

        if (CurrentGuideStep is { Index: var index } && index < steps.Count && steps[index].Point.Position == position)
        {
            return index;
        }

        return TraceTickTracks.GetLatestStepIndex(steps, position);
    }

    private int GetNextGuideIndex(int current, bool forward)
    {
        var steps = GetGuideSteps();

        var playhead = TraceTimelineTab.Playhead;

        if (current >= 0 && steps[current].Point.Position == playhead)
        {
            return forward ? current + 1 : current - 1;
        }

        return TraceTickTracks.GetStepIndex(steps, playhead, forward);
    }

    private int GetPhaseGuideIndex(int current, bool forward) => TraceTickTracks.GetPhaseStepIndex(GetGuideSteps(), current, forward);

    private int GetOperatorGuideIndex(int current, bool forward)
    {
        var items = GuideStepItems;

        if (forward)
        {
            var group = current >= 0 && current < items.Count ? items[current].Group : null;

            for (var index = current + 1; index < items.Count; index++)
            {
                if (!ReferenceEquals(items[index].Group, group))
                {
                    return index;
                }
            }

            return -1;
        }

        if (current <= 0 || current >= items.Count)
        {
            return -1;
        }

        var first = items[current].Group[0].Index;

        return first < current ? first : items[current - 1].Group[0].Index;
    }

    private void GoToGuideStep(int index)
    {
        var step = GetGuideSteps()[index];

        IsSelectingGuideStep = true;

        GuideStepIndex = index;

        IsSelectingGuideStep = false;

        SetCurrent(CurrentGuideStep, isCurrent: false);

        var item = index < GuideStepItems.Count ? GuideStepItems[index] : null;

        SetCurrent(item, isCurrent: true);

        CurrentGuideStep = item;

        TraceTimelineTab.SetPlayhead(step.Point.Position);

        TraceTimelineTab.RequestPlayhead(step.Point);
    }

    private List<TraceTrackStep> GetGuideSteps()
    {
        if (GuideSteps is not { } steps || !ReferenceEquals(GuideStepsHidden, _settingsViewModel.TraceTimelineHiddenTickTrackSet))
        {
            return RefreshGuideSteps();
        }

        return steps;
    }

    private List<TraceTrackStep> RefreshGuideSteps()
    {
        GuideStepsHidden = _settingsViewModel.TraceTimelineHiddenTickTrackSet;

        var steps = TraceTickTracks.GetSteps(TickTracks, GuideStepsHidden);

        GuideSteps = steps;

        GuideStepMaximum = Math.Max(0, steps.Count - 1);

        BuildGuideStepGroups(Layout.IsGuideMode ? steps : []);

        return steps;
    }

    private void BuildGuideStepGroups(List<TraceTrackStep> steps)
    {
        var items = new List<GuideStepItem>(steps.Count);

        var groups = new List<GuideStepGroup>();

        GuideStepGroup? group = null;

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];

            var (operatorName, operatorNode) = GetStepOperator(step);

            if (group is null || group.OperatorName != operatorName)
            {
                group = new GuideStepGroup(operatorName, operatorNode is null ? null : PlanIconResolver.Resolve(operatorNode));

                groups.Add(group);
            }

            var item = new GuideStepItem(index,
                                         TraceTickTracks.GetStepName(step),
                                         TraceTickTracks.GetStepDescription(step),
                                         step.Tick.Node.CategoryColour,
                                         group);

            group.Add(item);

            items.Add(item);
        }

        CurrentGuideStep = null;

        GuideStepItems = items;

        GuideStepGroups = groups;
    }

    private static void SetCurrent(GuideStepItem? item, bool isCurrent)
    {
        if (item is null)
        {
            return;
        }

        item.IsCurrent = isCurrent;

        item.Group.IsCurrent = isCurrent;
    }

    private (string Name, PlanNode? Node) GetStepOperator(TraceTrackStep step)
    {
        var operatorEvent = step.Tick.Operator;

        if (step.IsReturn || step.Rule.Source != TraceTickSourceType.RowReturn)
        {
            return (operatorEvent.Label, GetPlanNode(operatorEvent));
        }

        if (step.Tick.Caller is { } caller)
        {
            return (caller.Label, GetPlanNode(caller));
        }

        return GetPlanNode(operatorEvent) is { } node && FindParent(ExecutionPlans.SelectMany(p => p.Root), node) is { } parent
            ? (parent.PhysicalOperator, parent)
            : (operatorEvent.Label, GetPlanNode(operatorEvent));
    }

    private PlanNode? GetPlanNode(ExecutionOperatorEvent operatorEvent)
        => operatorEvent.PlanNodeIdentifier is { } identifier ? ResolvePlanNode(identifier) : null;

    private void ResetGuide()
    {
        CreatedGuidePlayback?.Stop();

        SetCurrent(CurrentGuideStep, isCurrent: false);

        CurrentGuideStep = null;

        IsSelectingGuideStep = true;

        GuideStepIndex = 0;

        IsSelectingGuideStep = false;
    }

    partial void OnTickTracksChanged(IReadOnlyList<TraceTickTrack> value)
    {
        if (RowFlowPosition is { } position)
        {
            RowFlowPosition = null;

            UpdatePlanActivity(position);
        }

        GuideSteps = null;

        if (Layout.IsGuideMode)
        {
            RefreshGuideSteps();
        }
        else
        {
            BuildGuideStepGroups([]);
        }
    }

    partial void OnGuideStepIndexChanged(double value)
    {
        if (IsSelectingGuideStep || Trace is null)
        {
            return;
        }

        var index = (int)Math.Round(value);

        if (index >= 0 && index < GetGuideSteps().Count)
        {
            GuidePlayback.Stop();

            GoToGuideStep(index);
        }
    }
}
