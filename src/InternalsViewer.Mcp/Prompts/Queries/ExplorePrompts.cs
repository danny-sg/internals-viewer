using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Prompts.Queries;

[McpServerPromptType]
public sealed class ExplorePrompts
{
    [McpServerPrompt(Name = "explore", Title = "Explore The Current Query")]
    [Description("Explore the query run open in Internals Viewer, optionally focused on a question and scoped to what is on " +
                 "screen or selected")]
    public static string Explore(
        [Description("The question to answer, for example why the query read so many pages")] string? question = null,
        [Description("What to cover: run (everything), visible (what the timelines are showing) or selection (the selected " +
                     "operator, node or page)")]
        string scope = "run")
    {
        var focus = string.IsNullOrWhiteSpace(question) ? "Explain what SQL Server did to run it and where the cost went." : question.Trim();

        var scoping = scope.Trim().ToLowerInvariant() switch
        {
            "run" => string.Empty,
            "visible" => """

                Scope the investigation to what the user can see. Call get_selection first and take the ranges it reports: pass the
                events timeline window as fromMs and toMs to query_events, query_reads, query_waits, query_locks and
                query_latches, and the trace timeline range as from and to on query_timeline, query_calls and
                query_memoryhistory, in the form the timeline is using, positions as hex
                Sequence:Steps or instruction counts. Describe the Full Trace in positions or instructions, never milliseconds.
                Mention what lies outside the range only when it changes the answer.
                """,
            "selection" => """

                Scope the investigation to what the user has selected. Call get_selection first and work from the operator, call
                tree node, event, page or slot it reports: query_calltree with operator or root for an operator or node, page_read
                for a page, query_events for an event. Explain that item rather than the whole run.
                """,
            _ => throw new McpException($"scope must be run, visible or selection, not '{scope}'.")
        };

        return $"""
            Explore the query run open in Internals Viewer. {focus}
            {scoping}
            Read the reading guide first (the guide tool with topic reading), then work from the evidence: list_tabs,
            get_selection to see what the user is looking at, then query_summary, query_plan (estimated against actual rows,
            warnings, the memory grant), query_reads (disk against buffer pool, read-ahead, extents), query_waits,
            query_operators (start, first row, duration, threads) and the call tree of the operators that matter. For a Full
            Trace, read the full-trace-investigations guide and go down to the functions, arguments and memory with
            query_calltree, query_calls, query_memory and query_memoryhistory. Lead with the answer, then the evidence with the
            ids the tools print, and open the key places in the app with the show_ tools as you explain them.
            """;
    }
}
