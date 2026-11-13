using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Plan.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    [ObservableProperty]
    private ObservableCollection<GuideStepGroup> _guideStepGroups = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentGuideMarker))]
    private GuideStepItem? _currentGuideStep;

    [ObservableProperty]
    private bool _isGuidePlaying;

    [ObservableProperty]
    private string _guidePhase = string.Empty;

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

    public bool IsGuideDetailed => _settingsViewModel.PlanDetailedSteps;

    public ITimelineTransport? TransportSource => Layout.IsGuideMode ? GuidePlayback : RulerSource;

    private GuideTransport? CreatedGuidePlayback { get; set; }

    private List<TraceTrackStep>? GuideSteps { get; set; }

    private List<GuideStepItem> GuideStepItems { get; set; } = [];

    private GuideStepTree GuideTree { get; set; } = new([], []);

    private IReadOnlySet<string>? GuideStepsAdditional { get; set; }

    private bool GuideStepsDetailed { get; set; }

    private List<(int Index, PlanNode Node)> GuidePhaseStarts { get; set; } = [];

    public void SelectGuideStep(GuideStepItem item) => SelectGuideStep(item.Index);

    public void SelectGuideStep(int index)
    {
        GuidePlayback.Stop();

        GoToGuideStep(index);
    }

    public void ToggleGuideStep(GuideStepItem item) => GuideTree.Toggle(item);

    public List<TraceTrackRule> GetAdditionalStepRules()
        => [.. TickTrackStore.Definitions.Where(d => !d.IsPlanDefault).DistinctBy(d => d.GetPlanStepKey())];

    public bool IsAdditionalStepSelected(TraceTrackRule rule)
        => _settingsViewModel.PlanAdditionalStepSet.Contains(rule.GetPlanStepKey());

    public void SetAdditionalStepSelected(TraceTrackRule rule, bool isSelected)
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

        RefreshGuideStepsAtPlayhead();
    }

    public void SetGuideDetailed(bool isDetailed)
    {
        _settingsViewModel.PlanDetailedSteps = isDetailed;

        RefreshGuideStepsAtPlayhead();
    }

    public bool CanSkipGuideTo(PlanNode node) => GetGuideSkipIndex(node) >= 0;

    public void SkipGuideTo(PlanNode node)
    {
        if (GetGuideSkipIndex(node) is >= 0 and var index)
        {
            SelectGuideStep(index);
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

    private int GetGuideSkipIndex(PlanNode node)
    {
        if (!Layout.IsGuideMode || Trace is null)
        {
            return -1;
        }

        var current = GetCurrentGuideIndex();

        var items = GuideStepItems;

        var group = current >= 0 && current < items.Count ? items[current].Group : null;

        for (var index = current + 1; index < items.Count; index++)
        {
            var next = items[index].Group;

            if (!ReferenceEquals(next, group) && ReferenceEquals(next.Node, node))
            {
                return index;
            }
        }

        return -1;
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

        ShowGuideStep(index);

        if (RowFlowPosition == step.Point.Position)
        {
            PlanActivity = PlanActivity with { Parts = GetPartActivity(null, step.Point.Position) };

            PlanActivityChanged?.Invoke(PlanActivity);
        }

        TraceTimelineTab.SetPlayhead(step.Point.Position);

        TraceTimelineTab.RequestPlayhead(step.Point);
    }

    private void FollowGuidePlayhead(TracePosition position)
    {
        if (!Layout.IsGuideMode || Trace is null)
        {
            return;
        }

        var steps = GetGuideSteps();

        if (CurrentGuideStep is { Index: var current } && current < steps.Count && steps[current].Point.Position == position)
        {
            return;
        }

        var index = TraceTickTracks.GetLatestStepIndex(steps, position);

        if (index < 0)
        {
            ResetGuide();

            return;
        }

        ShowGuideStep(index);
    }

    private void ShowGuideStep(int index)
    {
        SetCurrent(CurrentGuideStep, isCurrent: false);

        var item = index < GuideStepItems.Count ? GuideStepItems[index] : null;

        if (item is not null)
        {
            GuideTree.Reveal(item);
        }

        SetCurrent(item, isCurrent: true);

        CurrentGuideStep = item;

        GuidePhase = GetGuidePhase(index);
    }

    private void SetGuidePositionText()
    {
        var origin = TraceTimeline?.Start.Position.Sequence ?? 0;

        foreach (var item in GuideStepItems)
        {
            item.PositionText = TracePositionText.GetRulerText(item.Position, origin);
        }
    }

    private TraceTrackStep? GetGuideStepAt(TracePosition position)
        => CurrentGuideStep is { Index: var index }
           && GuideSteps is { } steps
           && index < steps.Count
           && steps[index].Point.Position == position
            ? steps[index]
            : null;

    private List<TraceTrackStep> GetGuideSteps()
    {
        if (GuideSteps is not { } steps
            || !ReferenceEquals(GuideStepsAdditional, _settingsViewModel.PlanAdditionalStepSet)
            || GuideStepsDetailed != _settingsViewModel.PlanDetailedSteps)
        {
            return RefreshGuideSteps();
        }

        return steps;
    }

    private List<TraceTrackStep> RefreshGuideSteps()
    {
        GuideStepsAdditional = _settingsViewModel.PlanAdditionalStepSet;

        GuideStepsDetailed = _settingsViewModel.PlanDetailedSteps;

        var steps = TraceTickTracks.GetPlanSteps(TickTracks, GuideStepsAdditional, GuideStepsDetailed);

        GuideSteps = steps;

        GuidePhaseStarts = GetPhaseStarts(steps);

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
                                         TraceTickTracks.IsStickyStep(step, index, nesting),
                                         step.Point.Position);

            group.Add(item);

            items.Add(item);
        }

        CurrentGuideStep = null;

        GuidePhase = string.Empty;

        GuideStepItems = items;

        GuideStepGroups = groups;

        GuideTree = new GuideStepTree(items, groups);

        SetGuidePositionText();
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

    private List<(int Index, PlanNode Node)> GetPhaseStarts(List<TraceTrackStep> steps)
    {
        var starts = new List<(int Index, PlanNode Node)>();

        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index] is { Rule.IsPhase: true, IsReturn: false } step
                && GetPlanNode(step.Tick.Operator) is { IsStatement: false } node)
            {
                starts.Add((index, node));
            }
        }

        return starts;
    }

    private string GetGuidePhase(int index)
    {
        if (GuideSteps is not { } steps || index < 0 || index >= steps.Count)
        {
            return string.Empty;
        }

        var position = steps[index].Point.Position;

        for (var node = GetStepOperator(steps[index]).Node; node is not null; node = GetPlanParent(node))
        {
            if (GetOperatorPhase(steps, index, node, position) is { } phase)
            {
                return phase;
            }
        }

        return string.Empty;
    }

    private string? GetOperatorPhase(List<TraceTrackStep> steps, int index, PlanNode node, TracePosition position)
    {
        for (var start = GuidePhaseStarts.Count - 1; start >= 0; start--)
        {
            var (phaseIndex, phaseNode) = GuidePhaseStarts[start];

            if (phaseIndex > index || !ReferenceEquals(phaseNode, node))
            {
                continue;
            }

            var phase = steps[phaseIndex];

            return phase.Rule.ReturnDescription is not null && phase.Tick.Span.To.Position < position
                ? null
                : TraceTickTracks.GetStepName(phase);
        }

        return null;
    }

    private void RefreshGuideStepsAtPlayhead()
    {
        GuidePlayback.Stop();

        var steps = RefreshGuideSteps();

        if (TraceTimelineTab.Playhead is { } position && TraceTickTracks.GetLatestStepIndex(steps, position) is >= 0 and var index)
        {
            GoToGuideStep(index);
        }
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

        GuidePhase = string.Empty;
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
        GuideSteps = null;

        if (Layout.IsGuideMode)
        {
            RefreshGuideSteps();
        }
        else
        {
            BuildGuideStepGroups([]);
        }

        if (RowFlowPosition is { } position)
        {
            RowFlowPosition = null;

            UpdatePlanActivity(position);
        }
    }
}
