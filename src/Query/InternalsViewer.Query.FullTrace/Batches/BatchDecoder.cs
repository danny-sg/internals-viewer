using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using InternalsViewer.Internals.BatchMode.Normalization;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Batches;

public sealed class BatchDecoder(DatabaseSource database, Func<ulong, IReadOnlyList<ColumnReference>?> getOutputColumns)
{
    private const string NullText = "NULL";

    private const decimal MoneyScale = 10000m;

    private ConcurrentDictionary<ulong, IReadOnlyList<ColumnReference>> Outputs { get; } = new();

    private ConcurrentDictionary<string, IReadOnlyDictionary<string, ColumnStructure>?> Tables { get; } = new();

    public string? GetColumnName(ulong iterator, int ordinal)
        => GetOutputColumn(iterator, ordinal) is { Column.Length: > 0 } column ? column.Column : null;

    public ColumnStructure? GetColumn(ulong iterator, int ordinal)
        => GetOutputColumn(iterator, ordinal) is { } column ? GetStructure(column) : null;

    public string? Decode(ulong iterator, int ordinal, ulong slot, BatchDeepValue? deepValue)
        => GetColumn(iterator, ordinal) is { } structure ? DecodeSlot(slot, deepValue, structure) : null;

    public static string? DecodeSlot(ulong slot, BatchDeepValue? deepValue, ColumnStructure structure)
    {
        var value = new BatchValue((long)slot);

        var domain = BatchValueNormalizer.GetDomain(structure.DataType);

        try
        {
            return BatchValueDenormalizer.GetValueType(value, domain) switch
            {
                BatchValueType.Null => NullText,
                BatchValueType.DeepDataReference => DecodeDeepValue(deepValue, structure),
                BatchValueType.DictionaryReference => GetDictionaryText(value),
                _ => DecodeInline(value, domain, structure)
            };
        }
        catch (Exception exception) when (exception is NotSupportedException or OverflowException or ArgumentException)
        {
            return null;
        }
    }

    public static string GetDictionaryText(BatchValue value)
        => $"Dictionary {BatchValueDenormalizer.GetDictionaryNumber(value)} Entry {BatchValueDenormalizer.GetDictionaryEntry(value)}";

    private static string? DecodeInline(BatchValue value, BatchValueDomain domain, ColumnStructure structure)
    {
        if (domain == BatchValueDomain.Temporal)
        {
            return FormatTemporal(BatchValueDenormalizer.GetTemporalValue(value, structure.DataType));
        }

        var storage = BatchValueDenormalizer.GetStorageValue(value, domain);

        return domain switch
        {
            BatchValueDomain.Real when structure.DataType == SqlDbType.Real
                => ((float)BitConverter.Int64BitsToDouble(storage)).ToString(CultureInfo.InvariantCulture),
            BatchValueDomain.Real
                => BitConverter.Int64BitsToDouble(storage).ToString("R", CultureInfo.InvariantCulture),
            BatchValueDomain.Numeric
                => GetDecimal(storage, structure).ToString(),
            BatchValueDomain.Integer
                => DecodeInteger(storage, structure),
            _ => null
        };
    }

    private static string DecodeInteger(long storage, ColumnStructure structure)
    {
        switch (structure.DataType)
        {
            case SqlDbType.Money or SqlDbType.SmallMoney:
                return (storage / MoneyScale).ToString(CultureInfo.InvariantCulture);

            case SqlDbType.DateTime or SqlDbType.SmallDateTime:
                Span<byte> buffer = stackalloc byte[sizeof(long)];

                BinaryPrimitives.WriteInt64LittleEndian(buffer, storage);

                var length = structure.DataType == SqlDbType.DateTime ? sizeof(long) : sizeof(int);

                return DataConverter.BinaryToString(buffer[..length], structure.DataType);

            default:
                return storage.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static SqlDecimal GetDecimal(long storage, ColumnStructure structure)
    {
        var magnitude = storage < 0 ? (ulong)-storage : (ulong)storage;

        return new SqlDecimal(structure.Precision,
                              structure.Scale,
                              storage >= 0,
                              (int)(magnitude & 0xFFFFFFFF),
                              (int)(magnitude >> 32),
                              0,
                              0);
    }

    private static string? DecodeDeepValue(BatchDeepValue? deepValue, ColumnStructure structure)
    {
        if (deepValue is not { IsReadable: true, IsTruncated: false }
            || structure.DataType is not (SqlDbType.Char
                                          or SqlDbType.VarChar
                                          or SqlDbType.NChar
                                          or SqlDbType.NVarChar
                                          or SqlDbType.Binary
                                          or SqlDbType.VarBinary))
        {
            return null;
        }

        return DataConverter.BinaryToString(deepValue.Data, structure.DataType, structure.Precision, structure.Scale);
    }

    private static string? FormatTemporal(object value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime moment => moment.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset moment => moment.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
        TimeSpan span => span.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture),
        _ => null
    };

    private ColumnReference? GetOutputColumn(ulong iterator, int ordinal)
    {
        var columns = Outputs.GetOrAdd(iterator, i => getOutputColumns(i) ?? []);

        return ordinal >= 0 && ordinal < columns.Count ? columns[ordinal] : null;
    }

    private ColumnStructure? GetStructure(ColumnReference column)
    {
        if (column.Table.Length == 0)
        {
            return null;
        }

        var columns = Tables.GetOrAdd(column.TableKey, _ => LoadTable(column.Schema, column.Table));

        return columns?.GetValueOrDefault(column.Column);
    }

    private IReadOnlyDictionary<string, ColumnStructure>? LoadTable(string schema, string table)
    {
        if (AllocationUnitProvider.FindTableAllocationUnit(database, schema, table) is not { } allocationUnit)
        {
            return null;
        }

        return TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId)
                                     .Columns
                                     .Where(c => !c.IsDropped)
                                     .GroupBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }
}
