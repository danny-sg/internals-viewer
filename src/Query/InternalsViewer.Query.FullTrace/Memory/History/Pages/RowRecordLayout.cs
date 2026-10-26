using System.Buffers.Binary;
using System.Data;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Query.FullTrace.Memory.History.Pages;

public sealed class RowRecordLayout
{
    private const int HeaderSize = 4;

    private const int FixedEndOffset = 2;

    private const string KeySuffix = " (Build Key)";

    public RowRecordLayout(List<ColumnStructure> columns, int keyCount = 0)
    {
        Columns = columns;

        KeyCount = Math.Clamp(keyCount, 0, columns.Count);

        FixedLength = columns.Where(c => !IsVariableLength(c)).Sum(c => (int)c.DataLength);

        Structure = BuildStructure(columns);
    }

    public List<ColumnStructure> Columns { get; }

    public int KeyCount { get; }

    public int FixedLength { get; }

    public TableStructure Structure { get; }

    public static RowRecordLayout? Create(int count, int keyCount, Func<int, ColumnStructure?> getColumn)
    {
        var columns = new List<ColumnStructure>(count);

        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            if (getColumn(ordinal) is not { } column)
            {
                return null;
            }

            columns.Add(column);
        }

        return new RowRecordLayout(columns, keyCount);
    }

    public static bool IsVariableLength(ColumnStructure column)
        => column.DataLength < 0
           || column.DataType is SqlDbType.VarChar
                                 or SqlDbType.NVarChar
                                 or SqlDbType.VarBinary
                                 or SqlDbType.Text
                                 or SqlDbType.NText
                                 or SqlDbType.Image
                                 or SqlDbType.Xml
                                 or SqlDbType.Variant
                                 or SqlDbType.Udt;

    public bool IsKey(int ordinal) => ordinal >= Columns.Count - KeyCount && ordinal < Columns.Count;

    public bool Fits(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset + HeaderSize > data.Length)
        {
            return false;
        }

        var fixedEnd = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + FixedEndOffset)..]);

        return fixedEnd == HeaderSize + FixedLength
               && offset + fixedEnd + sizeof(ushort) <= data.Length
               && BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + fixedEnd)..]) == Columns.Count;
    }

    private TableStructure BuildStructure(List<ColumnStructure> columns)
    {
        var structure = new TableStructure(0);

        var fixedOffset = HeaderSize;

        var variableIndex = 0;

        for (var ordinal = 0; ordinal < columns.Count; ordinal++)
        {
            var column = columns[ordinal];

            var isVariableLength = IsVariableLength(column);

            structure.Columns.Add(column with
            {
                ColumnName = IsKey(ordinal) ? column.ColumnName + KeySuffix : column.ColumnName,
                ColumnId = (ushort)(ordinal + 1),
                LeafOffset = (short)(isVariableLength ? -++variableIndex : fixedOffset),
                NullBitIndex = (ushort)(ordinal + 1)
            });

            if (!isVariableLength)
            {
                fixedOffset += column.DataLength;
            }
        }

        return structure;
    }
}
