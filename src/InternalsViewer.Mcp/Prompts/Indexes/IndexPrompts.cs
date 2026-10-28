using System.ComponentModel;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Prompts.Indexes;

[McpServerPromptType]
public sealed class IndexPrompts
{
    [McpServerPrompt(Name = "index_seek", Title = "Seek A Key In An Index")]
    [Description("Walk the user through finding one key in an index, step by step in the Index tab")]
    public static string Seek(
        [Description("The key value to find, for example 1000")] string key,
        [Description("The index to seek in, as the table or index name. The index in the active Index tab when omitted")]
        string? index = null)
    {
        var target = GetTarget(index);

        return PromptTemplate.Render(PromptStore.Get("index-seek"), new { key = key.Trim(), target });
    }

    [McpServerPrompt(Name = "index_scan", Title = "Scan An Index In Key Order")]
    [Description("Walk the user through scanning an index in order, down one edge of the B-tree and along the leaf pages")]
    public static string Scan(
        [Description("The index to scan, as the table or index name. The index in the active Index tab when omitted")]
        string? index = null,
        [Description("ascending (down the left edge, following next page) or descending (down the right edge, following " +
                     "previous page)")]
        string direction = "ascending")
    {
        var descending = direction.Trim().StartsWith("desc", StringComparison.OrdinalIgnoreCase);

        var values = new
        {
            target = GetTarget(index),
            direction = descending ? "descending" : "ascending",
            edge = PromptStore.Get(descending ? "index-scan-descending" : "index-scan-ascending")
        };

        return PromptTemplate.Render(PromptStore.Get("index-scan"), values);
    }

    private static string GetTarget(string? index)
        => string.IsNullOrWhiteSpace(index) ? "the index open in the Index tab" : $"the index {index.Trim()}";
}