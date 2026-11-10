using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan.Enums;
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
    private ObservableCollection<GuideStepGroup> _guideStepGroups = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentGuideMarker))]
    private GuideStepItem? _currentGuideStep;

    [ObservableProperty]
    private bool _isGuidePlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentGuideMarker))]
    private bool _isGuideStepsVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentGuideMarker))]
    private bool _isGuideIndicatorVisible = true;

    public PlanStepMarker? CurrentGuideMarker
        => IsGuideIndicatorVisible && CurrentGuideStep is { Group.Node: { } node } step
            ? new PlanStepMarker(node, step.Placement, IsGuideStepsVisible ? null : step)
            : null;

    public GuideTransport GuidePlayback => CreatedGuidePlayback ??= CreateGuidePlayback();

    public ITimelineTransport? TransportSource => Layout.IsGuideMode ? GuidePlayback : RulerSource;

    private GuideTransport? CreatedGuidePlayback { get; set; }

    private List<TraceTrackStep>? GuideSteps { get; set; }

    private List<GuideStepItem> GuideStepItems { get; set; } = [];

    private GuideStepTree GuideTree { get; set; } = new([], []);

    private IReadOnlySet<string>? GuideStepsAdditional { get; set; }

    private bool IsSelectingGuideStep { get; set; }

    public void SelectGuideStep(GuideStepItem item) => SelectGuideStep(item.Index);

    public void SelectGuideStep(int index)
    {
        GuidePlayback.Stop();

        GoToGuideStep(index);
    }

    public void ToggleGuideStep(GuideStepItem item) => GuideTree.Toggle(item);

    public List<TraceTickDefinition> GetAdditionalStepRules()
        => [.. TickTrackStore.Definitions.Where(d => !d.IsPlanDefault).DistinctBy(d => d.GetPlanStepKey())];

    public bool IsAdditionalStepSelected(TraceTickDefinition rule)
        => _settingsViewModel.PlanAdditionalStepSet.Contains(rule.GetPlanStepKey());

    public void SetAdditionalStepSelected(TraceTickDefinition rule, bool isSelected)
    {
        var selected = new HashSet<string>(_settingsViewModel.PlanAdditionalStepSet, StringComparer.Ordinal);

        if (isSelected)
        {
            selected.Add(rule.GetPlanStepKey());
        }
        else
        {
            selected.Remove(rule.GetPlanStepKey());
        }

        _settingsViewModel.PlanAdditionalSteps = string.Join(SettingsViewModel.CategorySeparator, selected.Order(StringComparer.Ordinal));

        GuidePlayback.Stop();

        var steps = RefreshGuideSteps();

        if (TraceTimelineTab.Playhead is { } position && TraceTickTracks.GetLatestStepIndex(steps, position) is >= 0 and var index)
        {
            GoToGuideStep(index);
        }
    }

    [RelayCommand]
    private void GuidePlay() => GuidePlayback.TogglePlay();

    [RelayCommand]
    private void GuideNext() => GuidePlayback.Step(forward: true);

    [RelayCommand]
    private void GuidePrevious() => GuidePlayback.Step(forward: false);

    [RelayCommand]
    private void GuideStart()
    {
        GuidePlayback.Stop();

        GoToFirstGuideStep();
    }

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
        var playback = new GuideTransport(StepGuide, GoToFirstGuideStep);

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

        var next = current >= 0 && steps[current].Point.Position == playhead
            ? (forward ? current + 1 : current - 1)
            : TraceTickTracks.GetStepIndex(steps, playhead, forward);

        return GuideTree.GetShownIndex(next, forward);
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

        var first = items[current].Group.FirstIndex;

        return first < current ? first : items[current - 1].Group.FirstIndex;
    }

    private void SelectFirstGuideStep()
    {
        if (Layout.IsGuideMode)
        {
            GoToFirstGuideStep();
        }
    }

    private bool GoToFirstGuideStep()
    {
        if (Trace is null || GetGuideSteps().Count == 0)
        {
            return false;
        }

        GoToGuideStep(0);

        return true;
    }

    private void GoToGuideStep(int index)
    {
        var step = GetGuideSteps()[index];

        IsSelectingGuideStep = true;

        GuideStepIndex = index;

        IsSelectingGuideStep = false;

        SetCurrent(CurrentGuideStep, isCurrent: false);

        var item = index < GuideStepItems.Count ? GuideStepItems[index] : null;

        if (item is not null)
        {
            GuideTree.Reveal(item);
        }

        SetCurrent(item, isCurrent: true);

        CurrentGuideStep = item;

        TraceTimelineTab.SetPlayhead(step.Point.Position);

        TraceTimelineTab.RequestPlayhead(step.Point);
    }

    private List<TraceTrackStep> GetGuideSteps()
    {
        if (GuideSteps is not { } steps || !ReferenceEquals(GuideStepsAdditional, _settingsViewModel.PlanAdditionalStepSet))
        {
            return RefreshGuideSteps();
        }

        return steps;
    }

    private List<TraceTrackStep> RefreshGuideSteps()
    {
        GuideStepsAdditional = _settingsViewModel.PlanAdditionalStepSet;

        var steps = TraceTickTracks.GetPlanSteps(TickTracks, GuideStepsAdditional);

        GuideSteps = steps;

        GuideStepMaximum = Math.Max(0, steps.Count - 1);

        BuildGuideStepGroups(Layout.IsGuideMode ? steps : []);

        return steps;
    }

    private void BuildGuideStepGroups(List<TraceTrackStep> steps)
    {
        var items = new List<GuideStepItem>(steps.Count);

        var groups = new ObservableCollection<GuideStepGroup>();

        var nesting = TraceTickTracks.GetStepNesting(steps);

        GuideStepGroup? group = null;

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];

            var (operatorName, operatorNode) = GetStepOperator(step);

            var parent = nesting[index].Parent >= 0 ? items[nesting[index].Parent] : null;

            var depth = parent is null ? 0 : parent.Depth + 1;

            if (group is null || group.OperatorName != operatorName)
            {
                var icon = operatorNode is null ? null : PlanIconResolver.Resolve(operatorNode);

                group = new GuideStepGroup(operatorName, operatorNode, icon, index, depth);

                groups.Add(group);
            }

            var item = new GuideStepItem(index,
                                         TraceTickTracks.GetStepName(step),
                                         TraceTickTracks.GetStepDescription(step),
                                         TraceTickTracks.GetStepDetails(step),
                                         TraceTickTracks.GetStepCodeColour(step),
                                         group,
                                         parent,
                                         depth,
                                         nesting[index].End,
                                         TraceTickTracks.GetStepPredicate(step),
                                         GetStepPlacement(step),
                                         TraceTickTracks.IsStickyStep(step, index, nesting));

            group.Add(item);

            items.Add(item);
        }

        CurrentGuideStep = null;

        GuideStepItems = items;

        GuideStepGroups = groups;

        GuideTree = new GuideStepTree(items, groups);
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
        var isEvaluation = step.Rule.Source == TraceTickSourceType.Evaluate;

        var operatorEvent = isEvaluation && step.Tick.Caller is { } caller ? caller : step.Tick.Operator;

        var node = GetPlanNode(operatorEvent);

        return (node is { IsStatement: true } ? node.PhysicalOperator : operatorEvent.Label, node);
    }

    private static PlanStepPlacement GetStepPlacement(TraceTrackStep step)
    {
        if (step.Rule.Source != TraceTickSourceType.RowReturn)
        {
            return PlanStepPlacement.Operator;
        }

        return step.IsReturn ? PlanStepPlacement.Return : PlanStepPlacement.Call;
    }

    private PlanNode? GetPlanNode(ExecutionOperatorEvent operatorEvent)
        => operatorEvent.AdaptedNode is { } adapted
            ? ResolvePlanNode(adapted)?.Adapter
            : operatorEvent.PlanNodeIdentifier is { } identifier ? ResolvePlanNode(identifier) : null;

    private PlanNode? GetPlanParent(PlanNode node)
    {
        var parent = FindParent(ExecutionPlans.SelectMany(p => p.Root), node.IsAdapter ? node.Children[0] : node);

        while (parent is not null && OperatorClassifier.IsComputeScalar(parent))
        {
            parent = FindParent(ExecutionPlans.SelectMany(p => p.Root), parent);
        }

        return parent;
    }

    private void ResetGuide()
    {
        CreatedGuidePlayback?.Stop();

        SetCurrent(CurrentGuideStep, isCurrent: false);

        CurrentGuideStep = null;

        IsSelectingGuideStep = true;

        GuideStepIndex = 0;

        IsSelectingGuideStep = false;
    }

    partial void OnIsGuidePlayingChanged(bool value)
    {
        if (RowFlowPosition is { } position)
        {
            UpdatePlanActivity(position);
        }
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
