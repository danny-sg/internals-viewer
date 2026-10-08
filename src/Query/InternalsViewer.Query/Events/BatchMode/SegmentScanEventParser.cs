using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

internal static class SegmentScanEventParser
{
    public static SegmentScanEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var scan = new SegmentScanEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            ThreadId = e.GetInt("thread_id") ?? 0,
            NodeId = e.GetInt("node_id") ?? 0,
            RowGroupId = e.GetLong("rowgroup_id") ?? 0,
            ColumnId = e.GetInt("rowset_column_id") ?? 0,
            IsScanStart = !e.Name.EndsWith("finished", StringComparison.Ordinal)
        };

        if (e.Name.EndsWith("finished", StringComparison.Ordinal))
        {
            scan.InputRows = e.GetLong("input_rows_number") ?? 0;
            scan.OutputRows = e.GetLong("output_rows_number") ?? 0;
            scan.PureRowBuckets = e.GetLong("pure_row_buckets_number") ?? 0;
            scan.ImpureRowBuckets = e.GetLong("impure_row_buckets_number") ?? 0;

            return scan;
        }

        scan.EncodingType = (ColumnStoreEncodingType)(e.GetInt("encoding_type") ?? 0);
        scan.CompressedDataType = (ColumnStoreDataType)(e.GetInt("column_compressed_data_type") ?? 0);
        scan.SqlDataType = e.GetInt("column_sql_data_type") ?? 0;
        scan.FilterType = (ColumnStoreFilterType)(e.GetInt("filter_type") ?? 0);
        scan.FilterOnCompressedDataType = (ColumnStoreEarlyFilterType)(e.GetInt("filter_on_compressed_data_type") ?? 0);
        scan.BitPacking = e.GetInt("bit_packing") ?? 0;
        scan.BaseId = e.GetLong("base_id") ?? 0;
        scan.Magnitude = double.TryParse(e.GetString("magnitude"), out var magnitude) ? magnitude : 0;
        scan.NullValue = e.GetLong("null_value") ?? 0;
        scan.MinDataId = e.GetLong("metadata_min_data_id") ?? 0;
        scan.MaxDataId = e.GetLong("metadata_max_data_id") ?? 0;
        scan.PrimaryDictionaryValueCount = e.GetUInt("primary_dictionary_value_count") ?? 0;
        scan.SecondaryDictionaryValueCount = e.GetUInt("secondary_dictionary_value_count") ?? 0;
        scan.SecondaryBaseId = e.GetInt("secondary_base_id") ?? 0;
        scan.CpuInstructionSet = e.GetInt("cpu_instruction_set_used") is { } instructionSet
                                 ? (ColumnStoreInstructionSet)instructionSet
                                 : null;
        scan.IsFilterOnCompressedDataUsed = e.GetBool("is_filter_on_compressed_data_used") ?? false;
        scan.IsDeepDataPossible = e.GetBool("is_deep_data_possible") ?? e.GetBool("id_deep_data_possible") ?? false;
        scan.IsNullable = e.GetBool("is_nullable") ?? false;

        return scan;
    }
}
