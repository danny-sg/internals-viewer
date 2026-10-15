using System.Collections.Concurrent;
using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed class RowBufferDecoder(DatabaseSource database,
                                     Func<ulong, ExecutionOperatorEvent?> getOperator,
                                     Func<ulong, int, ColumnStructure?>? getOutputColumn = null)
{
    private const byte NullStatus = 3;

    private const string NullText = "NULL";

    private ConcurrentDictionary<ulong, IReadOnlyDictionary<int, ColumnStructure>?> Columns { get; } = new();

    public ColumnStructure? GetColumn(RowBuffer row, RowBufferColumn column)
        => row.Format == RowBufferFormat.Values
            ? getOutputColumn?.Invoke(row.Iterator, column.Ordinal)
            : GetColumns(row.Iterator)?.GetValueOrDefault(column.Binding.RecordOffset);

    public string? Decode(RowBuffer row, RowBufferColumn column)
    {
        if (GetColumn(row, column) is not { } structure)
        {
            return null;
        }

        if (column.Status == NullStatus)
        {
            return NullText;
        }

        if (column is not { Status: 0, IsUnreadable: false, IsTruncated: false, Page: null })
        {
            return null;
        }

        return row.Format == RowBufferFormat.Values
            ? DecodeValue(column, structure)
            : DataConverter.BinaryToString(column.Data, structure.DataType, structure.Precision, structure.Scale, structure.BitPosition);
    }

    internal static string? DecodeValue(RowBufferColumn column, ColumnStructure structure)
    {
        if (column.IsByReference)
        {
            return DataConverter.BinaryToString(column.Data, structure.DataType, structure.Precision, structure.Scale);
        }

        if (structure.DataType is SqlDbType.Char
                                  or SqlDbType.VarChar
                                  or SqlDbType.NChar
                                  or SqlDbType.NVarChar
                                  or SqlDbType.Binary
                                  or SqlDbType.VarBinary)
        {
            return string.Empty;
        }

        if (structure.DataLength <= 0 || structure.DataLength > column.Data.Length)
        {
            return null;
        }

        return DataConverter.BinaryToString(column.Data.AsSpan(0, structure.DataLength),
                                            structure.DataType,
                                            structure.Precision,
                                            structure.Scale);
    }

    internal static IReadOnlyDictionary<int, ColumnStructure> GetColumns(TableStructure structure)
        => structure.Columns
                    .Where(c => !c.IsDropped)
                    .GroupBy(c => (int)c.LeafOffset)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.Single());

    private IReadOnlyDictionary<int, ColumnStructure>? GetColumns(ulong iterator) => Columns.GetOrAdd(iterator, LoadColumns);

    private IReadOnlyDictionary<int, ColumnStructure>? LoadColumns(ulong iterator)
    {
        if (getOperator(iterator) is not { } operatorEvent
            || AllocationUnitProvider.FindAllocationUnit(database,
                                                         operatorEvent.OperatorSchemaName,
                                                         operatorEvent.OperatorTableName,
                                                         operatorEvent.OperatorIndexName) is not { IndexId: <= 1 } allocationUnit)
        {
            return null;
        }

        return GetColumns(TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId));
    }
}
