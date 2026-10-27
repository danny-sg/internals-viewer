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
        var focus = string.IsNullOrWhiteSpace(question)
            ? "Explain what SQL Server did to run it and where the cost went."
            : question.Trim();

        var scopeInstructions = scope.Trim().ToLowerInvariant() switch
        {
            "run" => string.Empty,
            "visible" => PromptStore.Get("explore-visible"),
            "selection" => PromptStore.Get("explore-selection"),
            _ => throw new McpException($"scope must be run, visible or selection, not '{scope}'.")
        };

        return PromptTemplate.Render(PromptStore.Get("explore"), new { focus, scopeInstructions });
    }
}