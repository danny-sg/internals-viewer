using System.Buffers.Binary;
using System.Text;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Batches;

[Trait("Category", "Unit")]
public class DeepDataReaderTests
{
    private const ulong FirstPage = 0x10000;

    private const ulong SecondPage = 0x20000;

    private const int PageSize = 0x40;

    [Fact]
    public void Pages_Follow_The_Next_Page_Link_While_The_Signature_Matches()
        => Assert.Equal([FirstPage, SecondPage], DeepDataReader.GetPages(GetMemory(), FirstPage));

    [Fact]
    public void Entries_Are_Read_From_The_Page_Header_In_Four_Byte_Steps()
    {
        var entries = DeepDataReader.ReadEntries(GetMemory(), FirstPage, PageSize);

        Assert.Equal([("Madrid", FirstPage + 0x18), ("P77", FirstPage + 0x24)],
                     entries.Select(e => (Encoding.Latin1.GetString(e.Data), e.Address)));
    }

    [Fact]
    public void A_Slot_Points_At_The_Data_After_Its_Length()
    {
        var entry = DeepDataReader.ReadValue(GetMemory(), FirstPage + 0x18 + DeepDataEntry.HeaderSize + 1)!;

        Assert.Equal(("Madrid", FirstPage + 0x18, 10), (Encoding.Latin1.GetString(entry.Data), entry.Address, entry.Size));
    }

    [Theory]
    [InlineData(0xFUL, false)]
    [InlineData(0x10000UL, false)]
    [InlineData(0x1001DUL, true)]
    public void Only_Odd_Values_Above_The_Inline_Tags_Are_References(ulong slot, bool isReference)
        => Assert.Equal(isReference, DeepDataReader.IsReference(slot));

    private static Memory GetMemory()
        => new Memory().Add(FirstPage, GetPage(SecondPage, "Madrid", "P77"))
                       .Add(SecondPage, GetPage(0, "Lisbon"));

    private static byte[] GetPage(ulong next, params string[] values)
    {
        var page = new byte[PageSize];

        BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(0x08), next);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(0x10), 0x53445042);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(0x14), PageSize - DeepDataReader.PageHeaderSize);

        var offset = DeepDataReader.PageHeaderSize;

        foreach (var value in values)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(offset), (ushort)value.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(offset + 2), (ushort)value.Length);

            Encoding.Latin1.GetBytes(value).CopyTo(page, offset + DeepDataEntry.HeaderSize);

            offset = (offset + DeepDataEntry.HeaderSize + value.Length + 3) & ~3;
        }

        return page;
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
