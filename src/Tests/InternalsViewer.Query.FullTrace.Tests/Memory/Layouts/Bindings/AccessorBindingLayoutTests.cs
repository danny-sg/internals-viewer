using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings.Enums;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;

[Trait("Category", "Unit")]
public class AccessorBindingLayoutTests
{
    private const ulong Header = 0x1000;

    private const ulong Columns = 0x2000;

    private const ulong Iterator = 0x3000;

    private const ulong RowBuffer = 0x4000;

    private const ulong ColumnAttributes = 0x5080;

    private const uint IntFlags = 0x81601;

    private const uint VarCharFlags = 0x81703;

    [Fact]
    public void Reads_The_Column_Count_And_The_Column_Bindings_Pointer()
        => Assert.Equal((2, Columns), AccessorBindingLayout.ReadHeader(GetMemory(), Header));

    [Fact]
    public void Reads_Each_Column_Binding()
    {
        var columns = AccessorBindingLayout.ReadColumns(GetMemory(), Columns, 2);

        Assert.Equal([(1, 4, 0x8, 0x0, 4, (byte)0x38, (ColumnBindingFlags)IntFlags),
                      (2, -1, 0x40, 0x20, 50, (byte)0xA7, (ColumnBindingFlags)VarCharFlags)],
                     columns.Select(c => (c.Ordinal, c.RecordOffset, c.ValueOffset, c.StatusOffset, c.MaximumLength, c.Type, c.Flags)));
    }

    [Fact]
    public void Length_Offset_Is_Read_From_The_Infrequent_Data_When_The_Length_Is_Bound()
        => Assert.Equal([null, 0x30], AccessorBindingLayout.ReadColumns(GetMemory(), Columns, 2).Select(c => c.LengthOffset));

    [Fact]
    public void A_Variable_Length_Column_Has_No_Record_Offset()
        => Assert.Equal([false, true], AccessorBindingLayout.ReadColumns(GetMemory(), Columns, 2).Select(c => c.IsVariableLength));

    [Fact]
    public void A_Variable_Length_Column_Is_Numbered_Among_The_Variable_Columns()
        => Assert.Equal([0, 1], AccessorBindingLayout.ReadColumns(GetMemory(), Columns, 2).Select(c => c.VariableIndex));

    [Fact]
    public void Reads_The_Column_Attributes_Pointer()
        => Assert.Equal([ColumnAttributes, ColumnAttributes],
                        AccessorBindingLayout.ReadColumns(GetMemory(), Columns, 2).Select(c => c.ColumnAttributes));

    [Fact]
    public void A_By_Reference_Value_Is_A_Pointer()
    {
        var flags = ColumnBindingFlags.Length | ColumnBindingFlags.ByReference;

        var column = new ColumnBinding(0, 1, -1, 0x8, 0x10, 0x0, 8000, 0xA7, flags, 0, 0);

        Assert.Equal(sizeof(ulong), column.ValueLength);
    }

    [Fact]
    public void The_Column_Count_Of_A_Column_Bindings_Array_Comes_From_Its_Size()
        => Assert.Equal([2, 4], new[] { 256, 512 }.Select(AccessorBindingLayout.GetColumnCount));

    [Fact]
    public void A_Scan_Holds_Its_Bindings_And_Its_Row_Buffer()
    {
        var scan = AccessorBindingLayout.ReadScan(GetMemory(), Iterator)!;

        Assert.Equal(new ScanBindingReference(Iterator, Header, Columns, RowBuffer), scan);
        Assert.True(scan.Contains(RowBuffer));
    }

    [Fact]
    public void An_Iterator_Without_Bindings_Is_No_Scan()
        => Assert.Null(AccessorBindingLayout.ReadScan(new Memory().Add(Iterator, new byte[0x90]), Iterator));

    private static Memory GetMemory()
    {
        var header = new byte[0x110];

        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x62), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x70), Columns);

        var columns = new byte[0x100];

        WriteColumn(columns, 0x00, 4, 1, 0x8, 0x0, 4, 0x38, IntFlags, Columns + 0xA0);
        WriteColumn(columns, 0x50, -1, 2, 0x40, 0x20, 50, 0xA7, VarCharFlags, Columns + 0xD0);

        BinaryPrimitives.WriteInt32LittleEndian(columns.AsSpan(0xD0 + 0x18), 0x30);

        var iterator = new byte[0x90];

        BinaryPrimitives.WriteUInt64LittleEndian(iterator.AsSpan(0x80), RowBuffer);
        BinaryPrimitives.WriteUInt64LittleEndian(iterator.AsSpan(0x88), Header);

        return new Memory().Add(Header, header)
                           .Add(Columns, columns)
                           .Add(Iterator, iterator);
    }

    private static void WriteColumn(byte[] data,
                                    int offset,
                                    int recordOffset,
                                    ushort ordinal,
                                    int valueOffset,
                                    int statusOffset,
                                    ushort maximumLength,
                                    byte type,
                                    uint flags,
                                    ulong infrequentData)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), recordOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 0x04), ordinal);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset + 0x18), ColumnAttributes);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x28), valueOffset);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x30), statusOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 0x3C), maximumLength);

        data[offset + 0x3E] = type;

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 0x40), flags);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset + 0x48), infrequentData);
    }

    private sealed class Memory : IMemoryReader
    {
        private List<(ulong Address, byte[] Data)> Regions { get; } = [];

        public Memory Add(ulong address, byte[] data)
        {
            Regions.Add((address, data));

            return this;
        }

        public int Read(ulong address, Span<byte> destination)
        {
            foreach (var (start, data) in Regions)
            {
                if (address >= start && address < start + (ulong)data.Length)
                {
                    var offset = (int)(address - start);

                    var count = Math.Min(destination.Length, data.Length - offset);

                    data.AsSpan(offset, count).CopyTo(destination);

                    return count;
                }
            }

            return 0;
        }
    }
}
