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
        var target = string.IsNullOrWhiteSpace(index) ? "the index open in the Index tab" : $"the index {index.Trim()}";

        return $"""
            Show the user how SQL Server finds the key {key.Trim()} in {target}, one visible step at a time. Read the index-seek
            guide first (the guide tool with topic index-seek) and follow it exactly: select the root page with
            index_select_page, read its records with index_records, binary search the slots and call index_select_row on each
            slot you probe, then select the child page of the last key at or below the target. Repeat down to the leaf, then
            open the page with show_page and select the row with page_select_slot. Read keys from index_records, never infer
            them from row counts. Say what you compared before each call and finish with the number of pages read.
            """;
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
        var target = string.IsNullOrWhiteSpace(index) ? "the index open in the Index tab" : $"the index {index.Trim()}";

        var descending = direction.Trim().StartsWith("desc", StringComparison.OrdinalIgnoreCase);

        var edge = descending ? "right edge, taking the last record at each level, then follow the previous page"
                              : "left edge, taking the first record at each level, then follow the next page";

        return $"""
            Show the user how SQL Server scans {target} in {(descending ? "descending" : "ascending")} key order, one visible
            step at a time. Read the index-scan guide first (the guide tool with topic index-scan) and follow it: go down the
            {edge} pointer in each leaf page header until it is 0:0. Use index_select_page, index_records and
            index_select_row for every step so the user sees it in the Index tab, and read the pointers from the tools, never
            infer them. Say what you are doing before each call and finish with the pages read and how many were out of
            physical order.
            """;
    }
}
