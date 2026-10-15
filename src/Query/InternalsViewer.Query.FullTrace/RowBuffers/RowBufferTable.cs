using System.Buffers;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

internal sealed partial class RowBufferTable
{
    private RowBufferTable(ulong function, ulong instance, Row[] rows, Value[] values, Binding[][] bindings, byte[] data)
    {
        Function = function;
        Instance = instance;
        Rows = rows;
        Calls = [.. rows.Select(r => r.Call)];
        Values = values;
        Bindings = bindings;
        Data = data;
    }

    public ulong Function { get; }

    public ulong Instance { get; }

    public int Count => Rows.Length;

    public int ColumnCount => Bindings.Length == 0 ? 0 : Bindings.Max(b => b.Length);

    private Row[] Rows { get; }

    private int[] Calls { get; }

    private Value[] Values { get; }

    private Binding[][] Bindings { get; }

    private byte[] Data { get; }

    public RowBuffer? GetRow(int call)
    {
        var index = Array.BinarySearch(Calls, call);

        return index < 0 ? null : CreateRow(index);
    }

    public bool TryGetIterator(int call, out ulong iterator)
    {
        var index = Array.BinarySearch(Calls, call);

        iterator = index < 0 ? 0 : Rows[index].Iterator;

        return index >= 0;
    }

    public IEnumerable<RowBuffer> GetRows()
    {
        for (var index = 0; index < Rows.Length; index++)
        {
            yield return CreateRow(index);
        }
    }

    public IReadOnlyList<int> GetCalls() => Calls;

    public (ulong Iterator, ulong Id) GetCapture(int index) => (Rows[index].Iterator, Rows[index].Id);

    public RowBuffer CreateRow(int index)
    {
        var row = Rows[index];

        var bindings = Bindings[row.Layout];

        var columns = new RowBufferColumn[bindings.Length];

        for (var ordinal = 0; ordinal < columns.Length; ordinal++)
        {
            var value = Values[row.FirstColumn + ordinal];

            var binding = bindings[ordinal];

            var data = Data.AsSpan(value.Offset..GetEnd(row.FirstColumn + ordinal));

            var page = value.Flags.HasFlag(RowBufferColumnFlags.PageReference) && data.Length == RowBufferPage.Size
                ? RowBufferPage.Parse(data)
                : null;

            columns[ordinal] = new RowBufferColumn(ordinal,
                                                   value.Status,
                                                   value.Flags,
                                                   value.Length,
                                                   binding.RecordOffset,
                                                   binding.Flags,
                                                   binding.FixedLength,
                                                   page is null ? [.. data] : [])
            {
                Page = page
            };
        }

        return new RowBuffer(Function, Instance, row.Call, row.Id, row.Iterator, row.Buffer, columns, row.Format);
    }

    private int GetEnd(int value) => value + 1 < Values.Length ? Values[value + 1].Offset : Data.Length;

    internal readonly record struct Row(int Call,
                                        ulong Id,
                                        ulong Iterator,
                                        ulong Buffer,
                                        int Layout,
                                        int FirstColumn,
                                        RowBufferFormat Format);

    internal readonly record struct Value(int Offset, int Length, byte Status, RowBufferColumnFlags Flags);

    internal readonly record struct Binding(int RecordOffset, uint Flags, int FixedLength);

    internal sealed class Builder(ulong function, ulong instance)
    {
        private List<Row> Rows { get; } = [];

        private List<Value> Values { get; } = [];

        private List<Binding[]> Bindings { get; } = [];

        private ArrayBufferWriter<byte> Data { get; } = new();

        public void Add(BufferRow row, ReadOnlySpan<BufferColumn> columns, ReadOnlySpan<byte> data)
        {
            Rows.Add(new Row((int)row.Call, row.Id, row.Iterator, row.Buffer, GetLayout(columns), Values.Count, row.Format));

            foreach (var column in columns)
            {
                Values.Add(new Value(Data.WrittenCount, (int)column.Length, column.Status, (RowBufferColumnFlags)column.Flags));

                Data.Write(data.Slice((int)column.Data, (int)column.Captured));
            }
        }

        public RowBufferTable Build()
            => new(function, instance, [.. Rows.OrderBy(r => r.Call)], [.. Values], [.. Bindings], [.. Data.WrittenSpan]);

        private int GetLayout(ReadOnlySpan<BufferColumn> columns)
        {
            for (var index = Bindings.Count - 1; index >= 0; index--)
            {
                if (IsLayout(Bindings[index], columns))
                {
                    return index;
                }
            }

            var bindings = new Binding[columns.Length];

            for (var ordinal = 0; ordinal < bindings.Length; ordinal++)
            {
                bindings[ordinal] = GetBinding(columns[ordinal]);
            }

            Bindings.Add(bindings);

            return Bindings.Count - 1;
        }

        private static bool IsLayout(Binding[] bindings, ReadOnlySpan<BufferColumn> columns)
        {
            if (bindings.Length != columns.Length)
            {
                return false;
            }

            for (var ordinal = 0; ordinal < bindings.Length; ordinal++)
            {
                if (bindings[ordinal] != GetBinding(columns[ordinal]))
                {
                    return false;
                }
            }

            return true;
        }

        private static Binding GetBinding(BufferColumn column) => new(column.RecordOffset, column.BindingFlags, column.FixedLength);
    }
}
