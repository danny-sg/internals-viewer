using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline;

public sealed partial class QueryTraceTimelineTabView : ITraceTimelineController
{
    private const string PaneGroup = "Pane";

    private const string ScaleOptionGroup = "Scale";

    private const string MemoryOptionGroup = "Flame Chart Memory";

    private const string RegionGroup = "Highlight Regions";

    private const string HighlightPrefix = "highlight-";

    private const int MaximumSteps = 100;

    private static readonly TimelineOption[] TimelineOptions =
    [
        new("pane-operators",
            "Operators",
            PaneGroup,
            v => v.TraceTimeline.ShowOperators,
            (v, b) => v.SetPane(b, () => v.TraceTimeline.ShowOperators = b)),
        new("pane-memory",
            "Memory",
            PaneGroup,
            v => v.TraceTimeline.ShowMemoryHistory,
            (v, b) => v.SetPane(b, () => v.TraceTimeline.ShowMemoryHistory = b)),
        new("pane-calls",
            "Calls",
            PaneGroup,
            v => v.TraceTimeline.ShowFlameChart,
            (v, b) => v.SetPane(b, () => v.TraceTimeline.ShowFlameChart = b)),
        new("playing", "Playing", "Playback", v => v.TraceTimeline.IsPlaying, (v, b) => v.SetPlaying(b)),
        new("scale-position",
            "Position",
            ScaleOptionGroup,
            v => v.TraceTimeline.Axis == TraceTimelineAxis.Position,
            (v, _) => v.TraceTimeline.Axis = TraceTimelineAxis.Position,
            IsChoice: true),
        new("scale-instructions",
            "Instructions",
            ScaleOptionGroup,
            v => v.TraceTimeline.Axis == TraceTimelineAxis.Instructions,
            (v, _) => v.TraceTimeline.Axis = TraceTimelineAxis.Instructions,
            IsChoice: true),
        new("memory-none",
            "None",
            MemoryOptionGroup,
            v => !v.TraceTimeline.ShowMemory,
            (v, _) => v.TraceTimeline.SetMemory(null),
            IsChoice: true),
        new("memory-in-use",
            "In Use",
            MemoryOptionGroup,
            v => v.TraceTimeline.ShowMemory && v.TraceTimeline.MemoryMode == TraceTimelineMemoryMode.InUse,
            (v, _) => v.TraceTimeline.SetMemory(TraceTimelineMemoryMode.InUse),
            IsChoice: true),
        new("memory-allocated",
            "Allocated",
            MemoryOptionGroup,
            v => v.TraceTimeline.ShowMemory && v.TraceTimeline.MemoryMode == TraceTimelineMemoryMode.Allocated,
            (v, _) => v.TraceTimeline.SetMemory(TraceTimelineMemoryMode.Allocated),
            IsChoice: true),
        new("call-highlight-hover",
            "Call Highlight Hover",
            "Memory History",
            v => v.TraceTimeline.HighlightMemoryWriters,
            (v, b) => v.TraceTimeline.HighlightMemoryWriters = b),
        new("call-stack", "Call Stack", "View", v => v.Tab?.IsCallStackVisible == true, (v, b) => v.Tab?.IsCallStackVisible = b),
        new("auto-zoom", "Auto-Zoom", "View", v => v.TraceTimeline.AutoZoom, (v, b) => v.TraceTimeline.AutoZoom = b),
        new("category-filter",
            "Category Filter",
            "View",
            v => v.CategoryFilter.IsPanelVisible,
            (v, b) => v.CategoryFilter.SetPanelVisible(b)),
        .. TraceTimelineControl.HighlightRegionLabels.Select(label => new TimelineOption(
            HighlightPrefix + label.ToLowerInvariant().Replace(' ', '-'),
            label,
            RegionGroup,
            v => v.TraceTimeline.IsHighlightRegionVisible(label),
            (v, b) => v.TraceTimeline.SetHighlightRegionVisible(label, b)))
    ];

    public McpTraceTimeline GetState()
    {
        var hidden = Settings.TraceTimelineHiddenTickTrackSet;

        return new McpTraceTimeline
        {
            Axis = TraceTimeline.Axis.ToString(),
            IsPlaying = TraceTimeline.IsPlaying,
            SearchText = string.IsNullOrEmpty(TraceTimeline.SearchText) ? null : TraceTimeline.SearchText,
            SearchMatches = TraceTimeline.SearchMatchCount,
            SelectedNode = TraceTimeline.GetSelectedCall()?.Node,
            SelectedCallIndex = TraceTimeline.GetSelectedCall()?.CallIndex ?? -1,
            Options = [.. TimelineOptions.Select(o => new McpOption(o.Key, o.Label, o.Group, o.Get(this), true))],
            Tracks =
            [
                .. (TraceTimeline.TickTracks ?? []).Select(t => new McpTimelineTrack(t.Name,
                                                                                    t.IsCustom,
                                                                                    !hidden.Contains(t.Name),
                                                                                    t.Series.Count,
                                                                                    t.Definition?.Module ?? string.Empty,
                                                                                    t.Definition?.Function ?? string.Empty,
                                                                                    [.. (t.Definition?.Conditions ?? []).Select(DescribeCondition)]))
            ]
        };
    }

    public string SetOption(string key, bool enabled)
    {
        var option = TimelineOptions.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"No Trace Timeline option '{key}'.");

        if (option.IsChoice && !enabled)
        {
            throw new InvalidOperationException($"{option.Label} is one of a choice for {option.Group}. Turn another one on instead.");
        }

        option.Set(this, enabled);

        return $"Turned {(enabled ? "on" : "off")} {option.Group} {option.Label}.";
    }

    public string SetTrackVisible(string name, bool isVisible)
    {
        var track = (TraceTimeline.TickTracks ?? []).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException(
                        $"No track '{name}'. The tracks are {string.Join(", ", (TraceTimeline.TickTracks ?? []).Select(t => t.Name))}.");

        OnTickTrackVisibilityChanged(track.Name, isVisible);

        return $"{(isVisible ? "Showing" : "Hid")} the {track.Name} track.";
    }

    public string Step(McpStepMode mode, bool forward, int count)
    {
        var stepMode = mode switch
        {
            McpStepMode.SameCall => PlayheadStepMode.SameCall,
            McpStepMode.Batch => PlayheadStepMode.Batch,
            McpStepMode.Row => PlayheadStepMode.Row,
            McpStepMode.Allocation => PlayheadStepMode.Allocation,
            _ => PlayheadStepMode.Call
        };

        if (stepMode == PlayheadStepMode.SameCall && TraceTimeline.GetSelectedCall() is null)
        {
            throw new InvalidOperationException("Same-call stepping needs a selected call. Select one with trace_goto_call first.");
        }

        for (var index = 0; index < Math.Clamp(count, 1, MaximumSteps); index++)
        {
            TraceTimeline.StepPlayhead(forward, stepMode);
        }

        return $"Stepped {(forward ? "forward" : "back")} by {mode}. {DescribePlayhead()}";
    }

    public string Search(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetSearchVisible(false);

            return "Cleared the search.";
        }

        SetSearchVisible(true);

        SearchBar.Text = text;

        TraceTimeline.SearchText = text;

        return $"Searching for '{text}'. {TraceTimeline.SearchMatchCount ?? 0} matching calls are visible.";
    }

    public string StepSearch(bool forward, int count)
    {
        if (string.IsNullOrEmpty(TraceTimeline.SearchText) || !TraceTimeline.HasSearchMatches)
        {
            throw new InvalidOperationException("There are no search matches to step through. Search first with trace_search.");
        }

        for (var index = 0; index < Math.Clamp(count, 1, MaximumSteps); index++)
        {
            TraceTimeline.StepSearch(forward);
        }

        return $"Moved to the {(forward ? "next" : "previous")} match. {DescribeSelection()}";
    }

    public string SelectCall(CallStackNode node, int callIndex, bool zoom)
    {
        if (!TraceTimeline.SelectCall(node, callIndex, zoom))
        {
            throw new InvalidOperationException(
                "That call is not visible in the Trace Timeline. A category filter or a scope may be hiding it.");
        }

        return $"Selected {DescribeCall(node, callIndex)}{(zoom ? " and zoomed to it" : string.Empty)}.";
    }

    public string Zoom(bool toSelection)
    {
        if (toSelection)
        {
            var call = TraceTimeline.GetSelectedCall()
                       ?? throw new InvalidOperationException("No call is selected. Select one with trace_goto_call first.");

            TraceTimeline.ZoomToCall(call.Node, call.CallIndex);

            return $"Zoomed to {DescribeCall(call.Node, call.CallIndex)}.";
        }

        TraceTimeline.ZoomToFit();

        return "Zoomed to fit the whole trace.";
    }

    private static string DescribeCall(CallStackNode node, int callIndex)
        => $"{(node.Frame is { } frame ? $"{frame.Module}!" : string.Empty)}{node.Symbol} call {callIndex}";

    private static string DescribeCondition(TraceCallCondition condition)
    {
        var subject = condition.Target switch
        {
            TraceConditionTarget.Argument => $"arg {condition.Argument?.Index}",
            TraceConditionTarget.Return => "return",
            _ => condition.Target.ToString()
        };

        return $"{subject} {condition.Operator} {condition.Value}";
    }

    private string DescribePlayhead()
        => TraceTimeline.GetPlayheadPosition() is { } position
            ? $"Playhead at {TracePositionText.GetHexText(position)}."
            : DescribeSelection();

    private string DescribeSelection()
        => TraceTimeline.GetSelectedCall() is { } call ? $"Selected {DescribeCall(call.Node, call.CallIndex)}." : string.Empty;

    private void SetPane(bool isVisible, Action set)
    {
        var visible = new[] { TraceTimeline.ShowOperators, TraceTimeline.ShowMemoryHistory, TraceTimeline.ShowFlameChart }.Count(v => v);

        if (!isVisible && visible <= 1)
        {
            throw new InvalidOperationException("At least one pane must stay visible. Turn another pane on first.");
        }

        set();
    }

    private void SetPlaying(bool isPlaying)
    {
        if (TraceTimeline.IsPlaying != isPlaying)
        {
            TraceTimeline.TogglePlay();
        }
    }

    private sealed record TimelineOption(string Key,
                                         string Label,
                                         string Group,
                                         Func<QueryTraceTimelineTabView, bool> Get,
                                         Action<QueryTraceTimelineTabView, bool> Set,
                                         bool IsChoice = false);
}
