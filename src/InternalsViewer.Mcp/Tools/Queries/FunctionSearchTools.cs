using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class FunctionSearchTools(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "trace_find_functions", ReadOnly = true)]
    [Description("Fuzzy search of the functions in the call graph, best matches first. Use it to resolve an ambiguous or half " +
                 "remembered name, or to find where something happens, before adding a track or going to a call. Every word must " +
                 "match, either contained in the name or with its letters in order. Reads the call tree without changing the UI")]
    public async Task<string> FindFunctions([Description("Words to look for, e.g. 'search page' or 'getrow'")] string text,
                                            [Description("Most matches to return")] int limit = 10,
                                            [Description("Query tab id from list_tabs; the active Query tab when omitted")]
                                            string? document = null,
                                            CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var matches = FunctionSearch.Search(scope.Context.Nodes.GetNodes(), text, Math.Clamp(limit, 1, 50));

        if (matches.Count == 0)
        {
            return $"no function in the call graph matches '{text}'";
        }

        var builder = new StringBuilder();

        foreach (var match in matches)
        {
            builder.Append(match.Name)
                   .Append("  nodes: ").Append(match.Nodes)
                   .Append("  calls: ").Append(match.Calls);

            if (match.Operator is not null)
            {
                builder.Append("  operator: ").Append(match.Operator);
            }

            if (match.Callers.Count > 0)
            {
                builder.Append("  called by: ").Append(string.Join(", ", match.Callers));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}
