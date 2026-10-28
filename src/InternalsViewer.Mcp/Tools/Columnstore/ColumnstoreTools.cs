using System.ComponentModel;
using System.Text;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Columnstore;

[McpServerToolType]
public sealed class ColumnstoreTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Columnstore tab id from list_tabs; the active Columnstore tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "columnstore_rowgroups", ReadOnly = true)]
    [Description("The row groups of the columnstore index open in the Columnstore tab: state, rows, deleted rows, size on disk " +
                 "and when each was created and closed")]
    public async Task<string> RowGroups([Description("Only this state: Open, Closed, Compressed, Tombstone etc.")]
                                        string? state = null,
                                        [Description("Most row groups to list")] int limit = 50,
                                        [Description("Row groups to skip")] int offset = 0,
                                        [Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Columnstore, tab, cancellationToken);

        var index = await GetIndexAsync(resolved, cancellationToken);

        var matching = index.RowGroups
                            .Where(r => state is null || r.State.ToString().Equals(state, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(r => r.RowGroupId)
                            .ToList();

        var builder = new StringBuilder();

        builder.AppendLine($"{index.SchemaName}.{index.TableName} {index.IndexName} " +
                           $"{(index.IsClustered ? "clustered" : "nonclustered")} columnstore in {resolved.DatabaseName}");

        builder.AppendLine($"  {index.RowGroups.Count} row groups, {Format.Number(index.TotalRows)} rows, " +
                           $"{Format.Bytes(index.TotalSize)} compressed, {index.Columns.Count} columns");

        var table = new TextTable("RowGroup", "State", "Rows", "Deleted", "Segments", "Size", "Created", "Closed");

        foreach (var rowGroup in matching.Skip(offset).Take(limit))
        {
            table.Add(rowGroup.RowGroupId,
                      rowGroup.State,
                      Format.Number(rowGroup.TotalRows),
                      rowGroup.DeletedRows,
                      rowGroup.Segments.Count,
                      Format.Bytes(rowGroup.SizeInBytes),
                      rowGroup.CreatedTime?.ToString("s"),
                      rowGroup.ClosedTime?.ToString("s"));
        }

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(matching.Count, offset + limit, "use offset and limit, or state"));

        return builder.ToString();
    }

    [McpServerTool(Name = "columnstore_segments", ReadOnly = true)]
    [Description("The column segments of the columnstore index: one per column per compressed row group, with the encoding, " +
                 "rows, size on disk, whether it has nulls, the min and max data id and its dictionaries")]
    public async Task<string> Segments([Description("Only this row group id")] int? rowGroup = null,
                                       [Description("Regex matched against the column name")] string? column = null,
                                       [Description("Most segments to list")] int limit = 50,
                                       [Description("Segments to skip")] int offset = 0,
                                       [Description(TabDescription)] string? tab = null,
                                       CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Columnstore, tab, cancellationToken);

        var index = await GetIndexAsync(resolved, cancellationToken);

        var pattern = Parse.Pattern(column);

        var matching = index.RowGroups
                            .Where(r => rowGroup is null || r.RowGroupId == rowGroup)
                            .OrderBy(r => r.RowGroupId)
                            .SelectMany(r => r.Segments.Select(s => (RowGroup: r, Segment: s)))
                            .Where(s => pattern.IsMatch(s.Segment.Column?.Name ?? string.Empty))
                            .OrderBy(s => s.RowGroup.RowGroupId)
                            .ThenBy(s => s.Segment.Column?.ColumnStoreColumnId)
                            .ToList();

        var table = new TextTable("RowGroup", "Column", "Encoding", "Rows", "Size", "Nulls", "MinId", "MaxId", "Dictionaries");

        foreach (var (group, segment) in matching.Skip(offset).Take(limit))
        {
            table.Add(group.RowGroupId,
                      segment.Column?.Name,
                      segment.Encoding,
                      Format.Number(segment.RowCount),
                      Format.Bytes(segment.OnDiskSize),
                      segment.HasNulls ? "yes" : "no",
                      segment.MinDataId,
                      segment.MaxDataId,
                      Dictionaries(segment));
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{matching.Count} segments");

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(matching.Count, offset + limit, "use offset and limit, or rowGroup and column"));

        return builder.ToString();
    }

    [McpServerTool(Name = "columnstore_dictionaries", ReadOnly = true)]
    [Description("The dictionaries of the columnstore index: the global dictionary of each column and the local dictionaries " +
                 "of its segments, with entry counts and size on disk")]
    public async Task<string> Dictionaries([Description("Regex matched against the column name")] string? column = null,
                                           [Description(TabDescription)] string? tab = null,
                                           CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Columnstore, tab, cancellationToken);

        var index = await GetIndexAsync(resolved, cancellationToken);

        var pattern = Parse.Pattern(column);

        var table = new TextTable("Column", "Scope", "Dictionary", "Entries", "Size", "RowGroups");

        foreach (var columnstoreColumn in index.Columns.Where(c => pattern.IsMatch(c.Name)))
        {
            if (columnstoreColumn.GlobalDictionary is { } global)
            {
                table.Add(columnstoreColumn.Name,
                          "global",
                          global.DictionaryId,
                          Format.Number(global.EntryCount),
                          Format.Bytes(global.OnDiskSize),
                          string.Empty);
            }

            var locals = index.RowGroups
                              .SelectMany(r => r.Segments.Where(s => s.Column == columnstoreColumn && s.LocalDictionary is not null)
                                                         .Select(s => (r.RowGroupId, Dictionary: s.LocalDictionary!)))
                              .ToList();

            foreach (var local in locals.Take(10))
            {
                table.Add(columnstoreColumn.Name,
                          "local",
                          local.Dictionary.DictionaryId,
                          Format.Number(local.Dictionary.EntryCount),
                          Format.Bytes(local.Dictionary.OnDiskSize),
                          local.RowGroupId);
            }

            if (locals.Count > 10)
            {
                table.Add(columnstoreColumn.Name, "local", $"{locals.Count - 10} more", string.Empty, string.Empty, string.Empty);
            }
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{table.Count} dictionaries");

        table.Write(builder);

        return builder.ToString();
    }

    private static string Dictionaries(ColumnSegment segment)
    {
        var parts = new List<string>();

        if (segment.PrimaryDictionaryId != 0)
        {
            parts.Add($"primary {segment.PrimaryDictionaryId}");
        }

        if (segment.SecondaryDictionaryId != 0)
        {
            parts.Add($"secondary {segment.SecondaryDictionaryId}");
        }

        if (segment.LocalDictionary is { } local)
        {
            parts.Add($"local {Format.Number(local.EntryCount)} entries");
        }

        return string.Join(", ", parts);
    }

    private static async Task<ColumnStoreIndex> GetIndexAsync(IMcpTab tab, CancellationToken cancellationToken)
    {
        var context = await tab.GetContextAsync(cancellationToken);

        return (context as ColumnstoreContext)?.Index
               ?? throw new McpException($"Columnstore tab {tab.Id} has not loaded its index yet.");
    }
}
