using System.ComponentModel;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Prompts;

[McpServerPromptType]
public sealed class ExplorePrompts
{
    [McpServerPrompt(Name = "explore", Title = "Explore The Current Query")]
    [Description("Explore the query run open in Internals Viewer, optionally focused on a question")]
    public static string Explore(
        [Description("The question to answer, for example why the query read so many pages")] string? question = null)
    {
        var focus = string.IsNullOrWhiteSpace(question) ? "Explain what SQL Server did to run it and where the cost went." : question.Trim();

        return $"""
            Explore the query run open in Internals Viewer. {focus}

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
