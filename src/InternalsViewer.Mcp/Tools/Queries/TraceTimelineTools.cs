using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class TraceTimelineTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private const string PanePrefix = "pane-";

    private const int SearchCandidates = 50;

    private const int MaximumListedMatches = 8;

    private const int MaximumPins = 2000;

    private const string ConditionsDescription = "Conditions on the calls: 'arg 2 = 0x1', 'return != 0', 'calledby Function', " +
                                                 "'calleddirectlyby Function' or 'operator Name'. Arguments are numbered from 1 " +
                                                 "as in query_calls. Operators are =, !=, <, <=, > and >=";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "trace_options", ReadOnly = true, Idempotent = true)]
    [Description("The state of the Trace Timeline: its panes (Operators, Memory, Calls), scale, memory mode, highlights, the " +
                 "operator tracks, the search and the selected call. Keys are for trace_set_option. Needs a Full Trace")]
    public async Task<string> Options([Description(TabDescription)] string? tab = null, CancellationToken cancellationToken = default)
    {
        var context = await Documents.GetContextAsync<QueryContext>(tab, cancellationToken);

        if (context.Timeline is not { } timeline)
        {
            return "The Trace Timeline is not open. Record a Full Trace first, or call trace_set_option with pane-operators.";
        }

        var builder = new StringBuilder();

        builder.AppendLine($"Trace Timeline of Query tab {context.TabId}");

        builder.AppendLine($"axis: {timeline.Axis}  playing: {(timeline.IsPlaying ? "yes" : "no")}");

        if (timeline.SearchText is { Length: > 0 })
        {
            builder.AppendLine($"search: '{timeline.SearchText}' with {timeline.SearchMatches ?? 0} matches");
        }

        if (timeline.SelectedNode is { } node)
        {
            builder.AppendLine($"selected call: {ConditionsText(node)} #{timeline.SelectedCallIndex}");
        }

        if (timeline.PinnedPositions > 0)
        {
            builder.AppendLine($"pinned: {Format.Number(timeline.PinnedPositions)} positions, cleared with trace_pin");
        }

        foreach (var group in timeline.Options.GroupBy(o => o.Group))
        {
            builder.AppendLine();

            builder.AppendLine(group.Key);

            var table = new TextTable("Key", "Option", "On", "Changeable");

            foreach (var option in group)
            {
                table.Add(option.Key, option.Label, option.IsOn ? "yes" : "no", option.IsEnabled ? "yes" : "no");
            }

            table.Write(builder);
        }

        builder.AppendLine();

        builder.AppendLine("Operator tracks");

        var tracks = new TextTable("Track", "Function", "Visible", "Custom", "Ticks");

        foreach (var track in timeline.Tracks)
        {
            tracks.Add(track.Name, track.Function, track.IsVisible ? "yes" : "no", track.IsCustom ? "yes" : "no", track.Ticks);
        }

        tracks.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "trace_set_option")]
    [Description("Turn a Trace Timeline toggle on or off: the Operators, Memory and Calls panes, playing, the scale, the memory " +
                 "mode, call highlighting, call stack, auto zoom, the category filter and the region highlights. Keys come from " +
                 "trace_options. Use Operators for the query's iterator view and Memory when looking at detail")]
    public async Task<string> SetOption([Description("Option key from trace_options")] string key,
                                        [Description("True to turn on, false to turn off")] bool enabled,
                                        [Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        if (RequireTimeline(session.Context) is { } timeline)
        {
            var option = timeline.Options.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))
                         ?? throw new McpException(
                             $"No Trace Timeline option '{key}'. Keys: {string.Join(", ", timeline.Options.Select(o => o.Key))}.");

            if (option.IsChoice && !enabled)
            {
                throw new McpException($"{option.Label} is one of a choice for {option.Group}. Turn another one on instead.");
            }

            if (!enabled && option.IsOn && option.Key.StartsWith(PanePrefix, StringComparison.Ordinal) &&
                timeline.Options.Count(o => o.Key.StartsWith(PanePrefix, StringComparison.Ordinal) && o.IsOn) <= 1)
            {
                throw new McpException("At least one pane must stay visible. Turn another pane on first.");
            }
        }

        await session.RunAsync<ITraceTimelineCommands>(c => c.SetTimelineOptionAsync(key, enabled), cancellationToken);

        var current = session.Context.Timeline?.Options.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));

        return current is null
            ? $"Turned {(enabled ? "on" : "off")} {key}."
            : $"Turned {(current.IsOn ? "on" : "off")} {current.Group} {current.Label}.";
    }

    [McpServerTool(Name = "trace_track_add")]
    [Description("Add a custom track to the Operators pane that marks every call of a function, e.g. 'search page' to show " +
                 "where a b-tree is searched. The function is resolved with a fuzzy search of the call graph and the track is " +
                 "named after it unless a name is given. Conditions narrow which calls are marked")]
    public async Task<string> AddTrack([Description("Function words, e.g. 'SearchPage' or 'sqlmin!BTreeRow'")] string function,
                                       [Description("Track name; the function name when omitted")] string? name = null,
                                       [Description("Hex colour such as #D13438; taken from the function's category when omitted")]
                                       string? colour = null,
                                       [Description(ConditionsDescription)] IReadOnlyList<string>? conditions = null,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(tab, cancellationToken);

        var timeline = RequireTimeline(scope.Query);

        var match = FunctionSearch.Resolve(scope.Context.Nodes.GetNodes(), function, false)[0];

        var node = match.Members[0];

        var trackName = name ?? node.Symbol;

        if (timeline?.Tracks.FirstOrDefault(t => t.IsCustom && string.Equals(t.Name, trackName, StringComparison.OrdinalIgnoreCase))
            is { } existing)
        {
            throw new McpException($"A track named '{existing.Name}' already exists. Remove it first or choose another name.");
        }

        var parsed = await ParseConditionsAsync(scope, node, conditions);

        var request = new McpTrackRequest(trackName, node.Frame!.Module, node.Symbol, colour, parsed);

        await scope.RunAsync<ITraceTimelineCommands>(c => c.AddTrackAsync(request), cancellationToken);

        return $"Added the {request.Name} track for {request.Module}!{request.Function}.";
    }

    [McpServerTool(Name = "trace_track_set")]
    [Description("Show or hide an Operators track, built in (GetRow, Evaluations, Reads) or custom")]
    public async Task<string> SetTrack([Description("Track name from trace_options")] string name,
                                       [Description("True to show, false to hide")] bool visible,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        if (RequireTimeline(session.Context) is { } timeline && FindTrack(timeline, name) is null)
        {
            throw new McpException($"No track '{name}'. The tracks are {string.Join(", ", timeline.Tracks.Select(t => t.Name))}.");
        }

        await session.RunAsync<ITraceTimelineCommands>(c => c.SetTrackVisibleAsync(name, visible), cancellationToken);

        var track = session.Context.Timeline is { } after ? FindTrack(after, name) : null;

        return $"{(track?.IsVisible ?? visible ? "Showing" : "Hid")} the {track?.Name ?? name} track.";
    }

    [McpServerTool(Name = "trace_track_remove")]
    [Description("Remove a custom Operators track by name, or all custom tracks when no name is given")]
    public async Task<string> RemoveTrack([Description("Custom track name; omit to clear all custom tracks")] string? name = null,
                                          [Description(TabDescription)] string? tab = null,
                                          CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        if (name is not null && RequireTimeline(session.Context) is { } timeline)
        {
            var track = FindTrack(timeline, name);

            if (track is null)
            {
                var custom = timeline.Tracks.Where(t => t.IsCustom).Select(t => t.Name).ToList();

                throw new McpException(
                    $"No custom track '{name}'. Custom tracks: {(custom.Count == 0 ? "none" : string.Join(", ", custom))}.");
            }

            if (!track.IsCustom)
            {
                throw new McpException($"{track.Name} is a built-in track. Built-in tracks can be hidden with trace_track_set.");
            }
        }

        var removed = await session.RunAsync<ITraceTimelineCommands, int>(c => c.RemoveTracksAsync(name), cancellationToken);

        return name is null
            ? $"Removed {removed} custom tracks."
            : $"Removed the {FindTrack(session.Before.Timeline, name)?.Name ?? name} track.";
    }

    [McpServerTool(Name = "trace_step")]
    [Description("Step the playhead: call (the next logged call), same-call (the next call of the selected function), " +
                 "batch, row or allocation")]
    public async Task<string> Step([Description("call, same-call, batch, row or allocation")] string mode = "call",
                                   [Description("True to step forward, false to step back")] bool forward = true,
                                   [Description("Steps to take, 1 to 100")] int count = 1,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var stepMode = mode.Trim().ToLowerInvariant().Replace("-", string.Empty).Replace(" ", string.Empty) switch
        {
            "call" => McpStepMode.Call,
            "samecall" => McpStepMode.SameCall,
            "batch" => McpStepMode.Batch,
            "row" => McpStepMode.Row,
            "allocation" => McpStepMode.Allocation,
            _ => throw new McpException($"Unknown step mode '{mode}'. Use call, same-call, batch, row or allocation.")
        };

        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        if (RequireTimeline(session.Context) is { SelectedNode: null } && stepMode == McpStepMode.SameCall)
        {
            throw new McpException("Same-call stepping needs a selected call. Select one with trace_goto_call first.");
        }

        await session.RunAsync<ITraceTimelineCommands>(c => c.StepAsync(stepMode, forward, count), cancellationToken);

        return $"Stepped {(forward ? "forward" : "back")} by {stepMode}. {DescribePlayhead(session.Context)}";
    }

    [McpServerTool(Name = "trace_search")]
    [Description("Search the timeline for text, highlighting the matches, or clear the search when no text is given. " +
                 "Step through the matches with the step option")]
    public async Task<string> Search([Description("Text to search for; omit to clear")] string? text = null,
                                     [Description("Step to the next (forward) or previous (false) match instead of searching")]
                                     bool? step = null,
                                     [Description("Matches to step over")] int count = 1,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        var timeline = RequireTimeline(session.Context);

        if (step is { } forward)
        {
            if (timeline is not { SearchText.Length: > 0, SearchMatches: > 0 })
            {
                throw new McpException("There are no search matches to step through. Search first with trace_search.");
            }

            await session.RunAsync<ITraceTimelineCommands>(c => c.StepSearchAsync(forward, count), cancellationToken);

            return $"Moved to the {(forward ? "next" : "previous")} match. {DescribeSelection(session.Context.Timeline)}";
        }

        await session.RunAsync<ITraceTimelineCommands>(c => c.SearchAsync(text), cancellationToken);

        return session.Context.Timeline is { SearchText.Length: > 0 } after
            ? $"Searching for '{after.SearchText}'. {after.SearchMatches ?? 0} matching calls are visible."
            : "Cleared the search.";
    }

    [McpServerTool(Name = "trace_zoom")]
    [Description("Zoom the timeline to the selected call, or fit the whole trace")]
    public async Task<string> Zoom([Description("True to zoom to the selected call, false to fit the whole trace")]
                                   bool toSelection = true,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        if (RequireTimeline(session.Context) is { SelectedNode: null } && toSelection)
        {
            throw new McpException("No call is selected. Select one with trace_goto_call first.");
        }

        await session.RunAsync<ITraceTimelineCommands>(c => c.ZoomAsync(toSelection), cancellationToken);

        return toSelection
            ? $"Zoomed to {DescribeSelection(session.Context.Timeline)}."
            : "Zoomed to fit the whole trace.";
    }

    [McpServerTool(Name = "trace_goto_call")]
    [Description("Select a call of a function on the timeline and optionally zoom to it, e.g. the first GetRow call. The function " +
                 "is resolved with a fuzzy search. 'which' is first, last, next or previous (relative to the playhead or " +
                 "selected call), or give n for the nth call, counted from 1. Conditions narrow the calls considered. An ambiguous " +
                 "name searches every best match and says which functions matched")]
    public async Task<string> GotoCall([Description("Function words, e.g. 'GetRow'")] string function,
                                       [Description("first, last, next or previous; ignored when n is given")] string which = "first",
                                       [Description("The nth matching call, counted from 1")] int? n = null,
                                       [Description(ConditionsDescription)] IReadOnlyList<string>? conditions = null,
                                       [Description("Zoom the timeline to the call")] bool zoom = true,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(tab, cancellationToken);

        var search = await SearchCallsAsync(scope, function, conditions);

        if (DescribeMissing(function, search) is { } missing)
        {
            return missing;
        }

        var candidates = search.Calls;

        var matches = search.Matches;

        var reference = scope.Query.Timeline?.SelectedNode is { } selected
                        ? candidates.Where(c => c.Node == selected && c.Index == scope.Query.Timeline.SelectedCallIndex)
                                    .Select(c => (TracePosition?)c.Call.FromPosition)
                                    .FirstOrDefault()
                        : null;

        var chosen = Choose(candidates, which, n, reference);

        await scope.RunAsync<ITraceTimelineCommands>(c => c.SelectCallAsync(chosen.Node, chosen.Index, zoom), cancellationToken);

        var after = (await scope.ReadContextAsync(cancellationToken)).Timeline;

        return $"Selected {DescribeSelection(after)}{(zoom ? " and zoomed to it" : string.Empty)}.{Environment.NewLine}" +
               $"{scope.Context.GetFunctionName(chosen.Node)} #{chosen.Index} of {candidates.Count} matching calls, " +
               $"{Format.Position(chosen.Call.FromPosition)} to {Format.Position(chosen.Call.ToPosition)}" +
               (matches.Count > 1
                   ? $"{Environment.NewLine}'{function}' matched {string.Join(", ", matches.Select(m => m.Name))}. " +
                     "Name one of them to narrow it."
                   : string.Empty);
    }

    [McpServerTool(Name = "trace_pin")]
    [Description("Pin marks on the Trace Timeline to show the user where things happen, e.g. every GetRow call. Give a function " +
                 "(resolved with a fuzzy search, narrowed by conditions) to pin the start of each of its calls, or positions to " +
                 "pin those, or both. Pins replace the previous ones. They are hidden when the user clicks the timeline or the " +
                 "playhead moves, so move the playhead first and pin last. Give neither to clear them. Pins outside the visible " +
                 "range appear on the minimap")]
    public async Task<string> Pin([Description("Function words, e.g. 'GetRow'")] string? function = null,
                                  [Description(ConditionsDescription)] IReadOnlyList<string>? conditions = null,
                                  [Description("Positions as hex Sequence:Steps, e.g. 1A3:4F")] IReadOnlyList<string>? positions = null,
                                  [Description(TabDescription)] string? tab = null,
                                  CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(tab, cancellationToken);

        var trace = scope.RequireTrace();

        if (string.IsNullOrWhiteSpace(function) && positions is not { Count: > 0 })
        {
            var cleared = scope.Query.Timeline?.PinnedPositions ?? 0;

            await scope.RunAsync<ITraceTimelineCommands>(c => c.PinPositionsAsync([]), cancellationToken);

            return cleared == 0 ? "There were no pins to clear." : $"Cleared {Format.Number(cleared)} pins.";
        }

        var points = new List<TracePoint>();

        var pinned = new List<string>();

        if (!string.IsNullOrWhiteSpace(function))
        {
            var search = await SearchCallsAsync(scope, function, conditions);

            if (DescribeMissing(function, search) is { } missing)
            {
                return missing;
            }

            if (search.Calls.Count > MaximumPins)
            {
                throw new McpException($"{Format.Number(search.Calls.Count)} calls match. Narrow them with conditions to " +
                                       $"{Format.Number(MaximumPins)} or fewer.");
            }

            foreach (var (node, index, _) in search.Calls)
            {
                if (trace.Timeline.FindCall(node, index) is { } location)
                {
                    points.Add(location.From);
                }
            }

            pinned.Add($"{Format.Number(points.Count)} calls of {string.Join(", ", search.Matches.Select(m => m.Name))}");
        }

        if (positions is { Count: > 0 })
        {
            points.AddRange(positions.Select(p => trace.Timeline.GetPoint(Parse.Position(p))));

            pinned.Add($"{Format.Number(positions.Count)} positions");
        }

        if (points.Count > MaximumPins)
        {
            throw new McpException($"That is {Format.Number(points.Count)} pins. Pin {Format.Number(MaximumPins)} or fewer.");
        }

        await scope.RunAsync<ITraceTimelineCommands>(c => c.PinPositionsAsync([.. points]), cancellationToken);

        var first = points.Min(p => p.Position);

        var last = points.Max(p => p.Position);

        return $"Pinned {string.Join(" and ", pinned)} on the Trace Timeline, from {Format.Position(first)} to " +
               $"{Format.Position(last)}.";
    }

    private async Task<CallSearch> SearchCallsAsync(McpDocumentScope scope, string function, IReadOnlyList<string>? conditions)
    {
        var trace = scope.RequireTrace();

        var found = FunctionSearch.Search(scope.Context.Nodes.GetNodes(), function, SearchCandidates);

        var matches = new List<FunctionMatch>();

        var candidates = new List<(CallStackNode Node, int Index, TraceArgumentCall Call)>();

        IReadOnlyList<TraceCallCondition> parsed = [];

        foreach (var tier in found.GroupBy(m => m.Score))
        {
            var tierMembers = tier.SelectMany(m => m.Members).ToList();

            parsed = await ParseConditionsAsync(scope, tierMembers[0], conditions);

            candidates = CollectCalls(trace, tierMembers, parsed);

            if (candidates.Count > 0)
            {
                matches = [.. tier.Where(m => m.Members.Any(member => candidates.Any(c => c.Node == member)))];

                break;
            }
        }

        candidates.Sort((left, right) => left.Call.FromPosition.CompareTo(right.Call.FromPosition));

        return new CallSearch(found, matches, candidates, parsed);
    }

    private static string? DescribeMissing(string function, CallSearch search)
    {
        if (search.Found.Count == 0)
        {
            return $"No function in the call graph matches '{function}'. Try trace_find_functions with fewer words.";
        }

        if (search.Calls.Count > 0)
        {
            return null;
        }

        return $"No call of {string.Join(", ", search.Found.Take(MaximumListedMatches).Select(m => m.Name))} is shown on the " +
               $"timeline{(search.Conditions.Count > 0 ? " that matches those conditions" : string.Empty)}. Instrumentation " +
               "wrappers are left off it.";
    }

    private static List<(CallStackNode Node, int Index, TraceArgumentCall Call)> CollectCalls(
        TraceResult trace,
        List<CallStackNode> members,
        IReadOnlyList<TraceCallCondition> parsed)
    {
        var calls = new List<(CallStackNode Node, int Index, TraceArgumentCall Call)>();

        foreach (var node in members)
        {
            if (node.Frame is not { } frame || trace.CallStore.GetCallList(frame.Address, frame.InstanceAddress) is not { } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (trace.CallStore.GetNode(call) != node || trace.Timeline.FindCall(node, index) is null)
                {
                    continue;
                }

                if (TraceCallCondition.MatchesAll(parsed, call) && parsed.Where(p => !p.IsCallCondition).All(p => Matches(p, node)))
                {
                    calls.Add((node, index, call));
                }
            }
        }

        return calls;
    }

    private static (CallStackNode Node, int Index, TraceArgumentCall Call) Choose(
        List<(CallStackNode Node, int Index, TraceArgumentCall Call)> candidates,
        string which,
        int? n,
        TracePosition? reference)
    {
        if (n is { } nth)
        {
            if (nth < 1 || nth > candidates.Count)
            {
                throw new McpException($"There are {candidates.Count} matching calls; n runs from 1 to {candidates.Count}.");
            }

            return candidates[nth - 1];
        }

        switch (which.Trim().ToLowerInvariant())
        {
            case "first":
                return candidates[0];

            case "last":
                return candidates[^1];

            case "next":
                return reference is { } after
                       ? candidates.FirstOrDefault(c => c.Call.FromPosition > after) is { Node: not null } next
                           ? next
                           : throw new McpException("There is no later matching call.")
                       : candidates[0];

            case "previous":
                return reference is { } before
                       ? candidates.LastOrDefault(c => c.Call.FromPosition < before) is { Node: not null } previous
                           ? previous
                           : throw new McpException("There is no earlier matching call.")
                       : candidates[^1];

            default:
                throw new McpException($"Unknown value '{which}' for which. Use first, last, next or previous, or give n.");
        }
    }

    private static bool Matches(TraceCallCondition condition, CallStackNode node)
    {
        return condition.Target switch
        {
            TraceConditionTarget.CalledBy or TraceConditionTarget.CalledDirectlyBy => condition.MatchesNode(node),
            _ => true
        };
    }

    private static string ConditionsText(CallStackNode node) => TraceConditions.Describe(node);

    private async Task<IReadOnlyList<TraceCallCondition>> ParseConditionsAsync(McpDocumentScope scope,
                                                                               CallStackNode node,
                                                                               IReadOnlyList<string>? conditions)
    {
        if (conditions is not { Count: > 0 })
        {
            return [];
        }

        ArgumentLayout? layout = null;

        if (TraceConditions.NeedsLayout(conditions) && node.Frame is { } frame)
        {
            try
            {
                await scope.RunAsync(async c => layout = (await c.ResolveSignatureAsync(frame))?.Layout, CancellationToken.None);
            }
            catch (McpException)
            {
            }
        }

        return TraceConditions.Parse(conditions, layout);
    }

    private static McpTimelineTrack? FindTrack(McpTraceTimeline? timeline, string name)
        => timeline?.Tracks.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static McpTraceTimeline? RequireTimeline(QueryContext context)
    {
        if (context.Options.Any(o => o is { Key: "trace-timeline", Unavailable: McpOptionUnavailable.NeedsFullTrace }))
        {
            throw new McpException("The Trace Timeline needs a Full Trace. Turn on full-trace, run the query and try again.");
        }

        return context.Timeline;
    }

    private static string DescribeSelection(McpTraceTimeline? timeline)
        => timeline?.SelectedNode is { } node ? $"{ConditionsText(node)} #{timeline.SelectedCallIndex}" : "no call";

    private static string DescribePlayhead(QueryContext context)
        => context.Selection.Playhead is { } position
            ? $"Playhead at {Format.Position(position)}."
            : $"Selected {DescribeSelection(context.Timeline)}.";

    private sealed record CallSearch(IReadOnlyList<FunctionMatch> Found,
                                     List<FunctionMatch> Matches,
                                     List<(CallStackNode Node, int Index, TraceArgumentCall Call)> Calls,
                                     IReadOnlyList<TraceCallCondition> Conditions);
}
