using System.ComponentModel;
using System.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Models.Context.Pages;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Pages;

[McpServerToolType]
public sealed class PageTools(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "page_read", ReadOnly = true)]
    [Description("Read the page open in a Page tab: its header, slot table and the records on it with their column values. " +
                 "Use show_page to open another page first")]
    public async Task<string> Page([Description("Only this slot's record, with every field")] int? slot = null,
                                   [Description("Records to list")] int limit = 50,
                                   [Description("First record to list")] int offset = 0,
                                   [Description("Width of a field value")] int width = 60,
                                   [Description("Tab id from list_tabs; the active Page tab when omitted")]
                                   string? document = null,
                                   CancellationToken cancellationToken = default)
    {
        var context = await Documents.GetContextAsync<PageContext>(document, cancellationToken);

        return Read(context, slot, limit, offset, width);
    }

    internal static string Read(PageContext context, int? slot, int limit, int offset, int width)
    {
        var loaded = context.Page ?? throw new McpException($"Tab {context.TabId} has no page open.");
        var pageAddress = loaded.PageAddress;

        var header = loaded.PageHeader;

        var builder = new StringBuilder();

        builder.AppendLine($"Page {pageAddress} {header.PageTypeName} level {header.Level} in {context.Database?.Name}");

        builder.AppendLine($"  allocation unit {header.AllocationUnitId} object {header.InternalObjectId} " +
                           $"index {header.InternalIndexId}");

        builder.AppendLine($"  previous {header.PreviousPage} next {header.NextPage}");

        builder.AppendLine($"  {header.SlotCount} slots, {header.FreeCount} bytes free, free data offset {header.FreeData}, " +
                           $"{header.GhostRecordCount} ghost records, fixed length size {header.FixedLengthSize}");

        builder.AppendLine($"  lsn {header.Lsn} flags 0x{header.FlagBits:X} type flags 0x{header.TypeFlagBits:X} " +
                           $"torn bits {header.TornBits}");

        if (context.Structures.Count > 0)
        {
            var structures = string.Join(", ", context.Structures.Select(s => $"{s.Name} = {s.Slot}"));

            builder.AppendLine($"  structures selectable as slots: {structures}");
        }

        var records = context.Records;

        if (records.Count == 0)
        {
            builder.AppendLine();

            var offsets = string.Join(", ", loaded.OffsetTable.Select((o, i) => $"{i}@{o}"));

            builder.AppendLine(loaded.OffsetTable.Length == 0 ? "No records on this page." : $"Slot offsets: {offsets}");

            return builder.ToString();
        }

        if (slot is { } wanted)
        {
            var record = records.FirstOrDefault(r => r.Slot == wanted)
                         ?? throw new McpException($"Page {pageAddress} has no slot {wanted}.");

            builder.AppendLine();

            builder.AppendLine(RecordLine(record));

            foreach (var field in record.Fields)
            {
                var value = field.IsNull ? "NULL" : field.Value;

                builder.AppendLine($"    {field.Name}: {value}  (offset {field.Offset}, {field.Length} bytes)");
            }

            return builder.ToString();
        }

        builder.AppendLine();

        builder.AppendLine($"{records.Count} records");

        foreach (var record in records.Skip(offset).Take(limit))
        {
            builder.AppendLine(RecordLine(record));

            var values = record.Fields.Select(f => $"{f.Name}={(f.IsNull ? "NULL" : Format.Truncate(f.Value, width))}");

            builder.AppendLine($"    {string.Join(", ", values)}");
        }

        builder.AppendIfNotEmpty(Format.More(records.Count, offset + limit, "use offset and limit, or slot for one record"));

        return builder.ToString();
    }

    private static string RecordLine(IRecord record)
    {
        var detail = string.Empty;

        if (record is IIndexRecord index)
        {
            var down = index.DownPagePointer.PageId != 0 ? $" down {index.DownPagePointer}" : string.Empty;

            var rid = index.Rid is { } identifier ? $" rid {identifier}" : string.Empty;

            detail = $" {index.NodeType}{down}{rid}";
        }

        var ghost = record.IsGhost ? " ghost" : string.Empty;

        return $"  slot {record.Slot} offset {record.Offset} {record.ColumnCount} columns{ghost}{detail}";
    }
}
