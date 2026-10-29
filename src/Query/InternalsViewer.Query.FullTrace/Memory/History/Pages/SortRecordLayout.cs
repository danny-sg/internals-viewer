using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Query.FullTrace.Memory.History.Pages;

public sealed class SortRecordLayout
{
    private const int StatusSize = 1;

    private const string KeySuffix = " (Sort Key)";

    public SortRecordLayout(List<ColumnStructure> columns, List<bool> keys)
    {
        FixedLength = StatusSize + columns.Where(c => !RowRecordLayout.IsVariableLength(c)).Sum(c => (int)c.DataLength);

        Columns = BuildColumns(columns, keys);
    }

    public List<IndexColumnStructure> Columns { get; }

    public int FixedLength { get; }

    public static SortRecordLayout? Create(int count, Func<int, ColumnStructure?> getColumn, Func<int, bool> isKey)
    {
        var columns = new List<ColumnStructure>(count);

        var keys = new List<bool>(count);

        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            if (getColumn(ordinal) is not { } column)
            {
                return null;
            }

            columns.Add(column);

            keys.Add(isKey(ordinal));
        }

        return new SortRecordLayout(columns, keys);
    }

    public bool Fits(PageHeader header) => header.FixedLengthSize == FixedLength;

    private static List<IndexColumnStructure> BuildColumns(List<ColumnStructure> columns, List<bool> keys)
    {
        var built = new List<IndexColumnStructure>(columns.Count);

        var fixedOffset = StatusSize;

        var variableIndex = 0;

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            var column = columns[ordinal];

            var isVariableLength = RowRecordLayout.IsVariableLength(column);

            built.Add(new IndexColumnStructure
            {
                ColumnName = keys[ordinal] ? column.ColumnName + KeySuffix : column.ColumnName,
                ColumnId = (ushort)(ordinal + 1),
                IndexColumnId = ordinal + 1,
                DataType = column.DataType,
                DataLength = column.DataLength,
                Precision = column.Precision,
                Scale = column.Scale,
                BitPosition = column.BitPosition,
                LeafOffset = (short)(isVariableLength ? -++variableIndex : fixedOffset),
                NullBitIndex = (ushort)(ordinal + 1),
                IsIndexKey = keys[ordinal]
            });

            if (!isVariableLength)
            {
                fixedOffset += column.DataLength;
            }
        }

        return built;
    }
}
