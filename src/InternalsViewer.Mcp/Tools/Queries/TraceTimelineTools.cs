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
        return await RunAsync(tab, c => c.SetTimelineOptionAsync(key, enabled), cancellationToken);
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

        var match = FunctionSearch.Resolve(scope.Context.Nodes.GetNodes(), function, false)[0];

        var node = match.Members[0];

        var parsed = await ParseConditionsAsync(scope, node, conditions);

        var request = new McpTrackRequest(name ?? node.Symbol, node.Frame!.Module, node.Symbol, colour, parsed);

        return await RunAsync(tab, c => c.AddTrackAsync(request), cancellationToken);
    }

    [McpServerTool(Name = "trace_track_set")]
    [Description("Show or hide an Operators track, built in (GetRow, Evaluations, Reads) or custom")]
    public async Task<string> SetTrack([Description("Track name from trace_options")] string name,
                                       [Description("True to show, false to hide")] bool visible,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        return await RunAsync(tab, c => c.SetTrackVisibleAsync(name, visible), cancellationToken);
    }

    [McpServerTool(Name = "trace_track_remove")]
    [Description("Remove a custom Operators track by name, or all custom tracks when no name is given")]
    public async Task<string> RemoveTrack([Description("Custom track name; omit to clear all custom tracks")] string? name = null,
                                          [Description(TabDescription)] string? tab = null,
                                          CancellationToken cancellationToken = default)
    {
        return await RunAsync(tab, c => c.RemoveTracksAsync(name), cancellationToken);
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

        return await RunAsync(tab, c => c.StepAsync(stepMode, forward, count), cancellationToken);
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
        if (step is { } forward)
        {
            return await RunAsync(tab, c => c.StepSearchAsync(forward, count), cancellationToken);
        }

        return await RunAsync(tab, c => c.SearchAsync(text), cancellationToken);
    }

    [McpServerTool(Name = "trace_zoom")]
    [Description("Zoom the timeline to the selected call, or fit the whole trace")]
    public async Task<string> Zoom([Description("True to zoom to the selected call, false to fit the whole trace")]
                                   bool toSelection = true,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        return await RunAsync(tab, c => c.ZoomAsync(toSelection), cancellationToken);
    }

    [McpServerTool(Name = "trace_goto_call")]
    [Description("Select a call of a function on the timeline and optionally zoom to it, e.g. the first GetRow call. The function " +
                 "is resolved with a fuzzy search. 'which' is first, last, next or previous (relative to the playhead or " +
                 "selected call), or give n for the nth call, counted from 1. Conditions narrow the calls considered")]
    public async Task<string> GotoCall([Description("Function words, e.g. 'GetRow'")] string function,
                                       [Description("first, last, next or previous; ignored when n is given")] string which = "first",
                                       [Description("The nth matching call, counted from 1")] int? n = null,
                                       [Description(ConditionsDescription)] IReadOnlyList<string>? conditions = null,
                                       [Description("Zoom the timeline to the call")] bool zoom = true,
                                       [Description("Take every function that matches equally well rather than failing when ambiguous")]
                                       bool allMatches = false,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(tab, cancellationToken);

        var store = scope.RequireTrace().CallStore;

        var matches = FunctionSearch.Resolve(scope.Context.Nodes.GetNodes(), function, allMatches);

        var members = matches.SelectMany(m => m.Members).ToList();

        var parsed = await ParseConditionsAsync(scope, members[0], conditions);

        var candidates = new List<(CallStackNode Node, int Index, TraceArgumentCall Call)>();

        lock (scope.Context.CallStoreSync)
        {
            foreach (var node in members)
            {
                if (node.Frame is not { } frame || store.GetCallList(frame.Address, frame.InstanceAddress) is not { } list)
                {
                    continue;
                }

                for (var index = 0; index < list.Count; index++)
                {
                    var call = list[index];

                    if (store.GetNode(call) != node)
                    {
                        continue;
                    }

                    if (TraceCallCondition.MatchesAll(parsed, call) && parsed.Where(p => !p.IsCallCondition).All(p => Matches(p, node)))
                    {
                        candidates.Add((node, index, call));
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            return $"No call of {matches[0].Name} matches{(parsed.Count > 0 ? " those conditions" : string.Empty)}.";
        }

        candidates.Sort((left, right) => left.Call.FromPosition.CompareTo(right.Call.FromPosition));

        var reference = scope.Query.Timeline?.SelectedNode is { } selected
                        ? candidates.Where(c => c.Node == selected && c.Index == scope.Query.Timeline.SelectedCallIndex)
                                    .Select(c => (TracePosition?)c.Call.FromPosition)
                                    .FirstOrDefault()
                        : null;

        var chosen = Choose(candidates, which, n, reference);

        var summary = await RunAsync(tab, c => c.SelectCallAsync(chosen.Node, chosen.Index, zoom), cancellationToken);

        return $"{summary}{Environment.NewLine}{matches[0].Name} #{chosen.Index} of {candidates.Count} matching calls, " +
               $"{Format.Position(chosen.Call.FromPosition)} to {Format.Position(chosen.Call.ToPosition)}";
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

    private Task<string> RunAsync(string? tab, Func<ITraceTimelineCommands, Task<string>> command, CancellationToken cancellationToken)
    {
        return Documents.RunAsync(McpArea.Query, tab, command, cancellationToken);
    }
}
