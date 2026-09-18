using InternalsViewer.Query.Events.Properties;
using System.Text;

namespace InternalsViewer.Query.Events.BatchMode;

public sealed partial record ColumnStoreScanEvent : EngineEvent
{
    [EventProperty("Event")]
    public string EventName { get; set; } = string.Empty;

    [EventProperty("Row Group")]
    public long? RowGroupId { get; set; }

    [EventProperty("Hobt Id")]
    public ulong HobtId { get; set; }

    [EventProperty("Detail")]
    public string Summary { get; set; } = string.Empty;

    public bool IsRowGroupRead
        => EventName is "column_store_rowgroup_read_issued" or "column_store_rowgroup_readahead_issued";

    public bool IsRowGroupReadAhead => EventName == "column_store_rowgroup_readahead_issued";

    public bool IsRowGroupEvent
        => IsRowGroupRead || EventName is "column_store_expression_filter_bitmap_set" or "column_store_rowgroup_skip_delete_buffer";

    public override string Name => DisplayName(EventName);

    public override string Description => Summary.Length == 0 ? Name : $"{Name} ({Summary})";

    public static string DisplayName(string eventName) => eventName switch
    {
        "column_store_rowgroup_read_issued" => "Rowgroup Read Issued",
        "column_store_rowgroup_readahead_issued" => "Rowgroup Read Ahead Issued",
        "column_store_fast_string_equals" => "Fast String Equals",
        "query_execution_wait_syncpoint" => "Batch Sync Point",
        _ => FormatName(eventName)
    };

    private static string FormatName(string eventName)
    {
        var trimmed = eventName;

        foreach (var prefix in new[] { "query_execution_column_store_", "query_execution_", "column_store_", "columnstore_" })
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                trimmed = trimmed[prefix.Length..];

                break;
            }
        }

        var builder = new StringBuilder(trimmed.Length);

        foreach (var word in trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToUpperInvariant(word[0])).Append(word, 1, word.Length - 1);
        }

        return builder.ToString();
    }
}
