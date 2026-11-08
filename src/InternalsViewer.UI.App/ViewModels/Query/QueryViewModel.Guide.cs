using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    private const string GuideHint = "Select Next to step to the first tick";

    private const int GuidePositionWidth = 90;

    private const int GuideOperatorWidth = 180;

    private const int GuideNameWidth = 160;

    private const int GuideDescriptionWidth = 420;

    [ObservableProperty]
    private string _guideName = string.Empty;

    [ObservableProperty]
    private string _guideDescription = GuideHint;

    [ObservableProperty]
    private string _guideOperatorName = string.Empty;

    [ObservableProperty]
    private Uri? _guideOperatorIcon;

    [ObservableProperty]
    private double _guideStepIndex;

    [ObservableProperty]
    private double _guideStepMaximum;

    [ObservableProperty]
    private string? _guideCodeColour;

    [ObservableProperty]
    private bool _isGuideStepsVisible = true;

    [ObservableProperty]
    private QueryResultSet? _guideStepsTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedGuideStep;

    public GuideTransport GuidePlayback => CreatedGuidePlayback ??= new GuideTransport(StepGuide);

    public ITimelineTransport? TransportSource => Layout.IsGuideMode ? GuidePlayback : RulerSource;

    private GuideTransport? CreatedGuidePlayback { get; set; }

    private List<TraceTrackStep>? GuideSteps { get; set; }

    private IReadOnlySet<string>? GuideStepsHidden { get; set; }

    private bool IsSelectingGuideStep { get; set; }

    [RelayCommand]
    private void GuideNext() => GuidePlayback.Step(forward: true);

    [RelayCommand]
    private void GuidePrevious() => GuidePlayback.Step(forward: false);

    private bool StepGuide(bool forward)
    {
        if (Trace is null)
        {
            return false;
        }

        var index = TraceTickTracks.GetStepIndex(GetGuideSteps(), TraceTimelineTab.Playhead, forward);

        if (index < 0)
        {
            return false;
        }

        GoToGuideStep(index);

        return true;
    }

    private void GoToGuideStep(int index)
    {
        var steps = GetGuideSteps();

        var step = steps[index];

        GuideCodeColour = step.Tick.Node.CategoryColour;

        GuideName = TraceTickTracks.GetStepName(step);

        GuideDescription = TraceTickTracks.GetStepDescription(step);

        var (operatorName, operatorNode) = GetStepOperator(step);

        GuideOperatorName = operatorName;

        GuideOperatorIcon = operatorNode is null ? null : PlanIconResolver.Resolve(operatorNode);

        IsSelectingGuideStep = true;

        GuideStepIndex = index;

        if (GuideStepsTable is { } table && index < table.Rows.Count)
        {
            SelectedGuideStep = table.Rows[index];
        }

        IsSelectingGuideStep = false;

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

        GuideStepsTable = Layout.IsGuideMode && IsGuideStepsVisible && Trace is { } trace ? BuildGuideStepsTable(trace, steps) : null;

        return steps;
    }

    private QueryResultSet BuildGuideStepsTable(TraceResult trace, List<TraceTrackStep> steps)
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Position", typeof(long), false) { Width = GuidePositionWidth, Alignment = ResultAlignment.Right },
            new(1, "Operator", typeof(string), false) { Width = GuideOperatorWidth },
            new(2, "Name", typeof(string), false) { Width = GuideNameWidth },
            new(3, "Description", typeof(string), false) { Width = GuideDescriptionWidth }
        };

        var origin = (QueryOptions.CropToQuery ? trace.QueryTimeline : trace.Timeline).GetStart(TraceTimelineAxis.Position);

        var scale = trace.Timeline.Scale;

        long GetTimelinePosition(TraceTrackStep step) => (long)Math.Floor(scale.GetValue(step.Point, TraceTimelineAxis.Position) - origin);

        var rows = steps.Select((s, index) => new ResultRow<long>([GetTimelinePosition(s),
                                                                   GetStepOperator(s).Name,
                                                                   TraceTickTracks.ToPlainText(TraceTickTracks.GetStepName(s)),
                                                                   TraceTickTracks.ToPlainText(TraceTickTracks.GetStepDescription(s))])
                                              {
                                                  Id = index
                                              })
                        .ToArray();

        return new QueryResultSet { Columns = columns, Rows = rows };
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

        GuideName = string.Empty;

        GuideDescription = GuideHint;

        GuideOperatorName = string.Empty;

        GuideOperatorIcon = null;

        GuideCodeColour = null;

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

        GuideStepsTable = null;

        if (Layout.IsGuideMode)
        {
            RefreshGuideSteps();
        }
    }

    partial void OnIsGuideStepsVisibleChanged(bool value) => RefreshGuideSteps();

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

    partial void OnSelectedGuideStepChanged(ResultRow<long>? value)
    {
        if (!IsSelectingGuideStep && value?.Id is { } index)
        {
            GoToGuideStep((int)index);
        }
    }
}
