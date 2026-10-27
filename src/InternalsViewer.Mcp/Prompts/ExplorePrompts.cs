using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Prompts;

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
                events timeline window as fromMs and toMs to events, reads, waits, locks and latches, and the trace timeline range
                as from and to on timeline, calls and memoryhistory, in the form the timeline is using, positions as hex
                Sequence:Steps or instruction counts. Describe the Full Trace in positions or instructions, never milliseconds.
                Mention what lies outside the range only when it changes the answer.
                """,
            "selection" => """

                Scope the investigation to what the user has selected. Call get_selection first and work from the operator, call
                tree node, event, page or slot it reports: calltree with operator or root for an operator or node, the page tool
                for a page, events for an event. Explain that item rather than the whole run.
                """,
            _ => throw new McpException($"scope must be run, visible or selection, not '{scope}'.")
        };

        return $"""
            Explore the query run open in Internals Viewer. {focus}
            {scoping}
            Read the reading guide first (the guide tool with topic reading), then work from the evidence: list_documents,
            get_selection to see what the user is looking at, then summary, the plan (estimated against actual rows,
            warnings, the memory grant), reads (disk against buffer pool, read-ahead, extents), waits, operators (start,
            first row, duration, threads) and the call tree of the operators that matter. For a Full Trace, read the
            full-trace-investigations guide and go down to the functions, arguments and memory with calltree, calls,
            memory and memoryhistory. Lead with the answer, then the evidence with the ids the tools print, and open the
            key places in the app with the show_ tools as you explain them.
            """;
    }
}
