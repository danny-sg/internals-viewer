using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryAccessTableTests
{
    private const int CacheSlots = 2;

    private static readonly MemoryHistoryAccessEntry[] Writes = [.. Enumerable.Range(0, 5000).Select(i => GetAccess(i, isRead: false))];

    private static readonly MemoryHistoryAccessEntry[] Reads = [.. Enumerable.Range(0, 5000).Select(i => GetAccess(i, isRead: true))];

    private static readonly byte[] Data = [.. Enumerable.Range(0, Writes.Sum(w => w.Length)).Select(i => (byte)(i * 31))];

    private static readonly int[] DataStarts = GetDataStarts();

    [Fact]
    public void Writes_And_Their_Bytes_Across_Several_Chunks_Come_Back_Unchanged()
        => AssertWrites(MemoryHistoryAccessTable.Build(Writes, Data, CacheSlots), Enumerable.Range(0, Writes.Length));

    [Fact]
    public void Writes_Come_Back_Unchanged_When_Visited_Out_Of_Order()
        => AssertWrites(MemoryHistoryAccessTable.Build(Writes, Data, CacheSlots), [4999, 0, 2047, 2048, 4096, 1, 4095, 3000]);

    [Fact]
    public void Reads_Come_Back_Unchanged_With_No_Bytes()
    {
        var table = MemoryHistoryAccessTable.Build(Reads, [], CacheSlots);

        var window = table.RentWindow();

        table.Read(100, 0, Reads.Length, window);

        Assert.Equal(Reads, Enumerable.Range(0, table.Count).Select(i => table[i]));
        Assert.All(Enumerable.Range(0, window.Count), i => Assert.True(window.GetBytes(i).IsEmpty));
    }

    [Fact]
    public void A_Window_Holds_The_Part_Of_The_Chunk_Inside_Its_Range()
    {
        var table = MemoryHistoryAccessTable.Build(Writes, Data, CacheSlots);

        var window = table.RentWindow();

        table.Read(2500, 2100, 3000, window);

        Assert.Equal((2100, 900), (window.First, window.Count));

        for (var index = 0; index < window.Count; index++)
        {
            var write = Writes[window.First + index];

            Assert.Equal(write, window.Entries[index]);

            Assert.Equal(Data.AsSpan(DataStarts[window.First + index], write.Length), window.GetBytes(index));
        }
    }

    [Fact]
    public void Writes_Stay_Unchanged_When_Threads_Share_Recycled_Cache_Slots()
    {
        var table = MemoryHistoryAccessTable.Build(Writes, Data, CacheSlots);

        Parallel.For(0, 8, thread => AssertWrites(table, Enumerable.Range(0, 2000).Select(i => (i * 7919 + thread * 613) % Writes.Length)));
    }

    [Fact]
    public void A_Table_Survives_An_Export_Round_Trip()
    {
        var table = MemoryHistoryAccessTable.Build(Writes, Data, CacheSlots);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            table.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        AssertWrites(MemoryHistoryAccessTable.Read(reader, CacheSlots), Enumerable.Range(0, Writes.Length));
    }

    [Fact]
    public void Writes_Longer_Than_Their_Data_Are_Rejected()
        => Assert.Throws<ArgumentException>(() => MemoryHistoryAccessTable.Build(Writes, new byte[4], CacheSlots));

    [Fact]
    public void An_Empty_Table_Has_No_Chunks()
    {
        var table = MemoryHistoryAccessTable.Build([], [], CacheSlots);

        Assert.Equal((0, 0L), (table.Count, table.Bytes));
    }

    private static void AssertWrites(MemoryHistoryAccessTable table, IEnumerable<int> indexes)
    {
        foreach (var index in indexes)
        {
            var bytes = new byte[Writes[index].Length];

            table.CopyBytes(index, 0, bytes);

            Assert.Equal(Writes[index], table[index]);

            Assert.Equal(Data.AsSpan(DataStarts[index], Writes[index].Length).ToArray(), bytes);
        }
    }

    private static int[] GetDataStarts()
    {
        var starts = new int[Writes.Length];

        for (var index = 1; index < starts.Length; index++)
        {
            starts[index] = starts[index - 1] + Writes[index - 1].Length;
        }

        return starts;
    }

    private static MemoryHistoryAccessEntry GetAccess(int index, bool isRead)
    {
        var block = index / 700;

        var position = new TracePosition((ulong)(1000 + block * 50 + index % 700 * 3), (ulong)(index * 7 % 13));

        var spanId = index % 11 == 0 ? -1 : 40_000 - block * 300 + index % 5;

        var type = isRead ? MemoryHistoryAccessType.Read
                   : index % 3 == 0 ? MemoryHistoryAccessType.Changed
                   : MemoryHistoryAccessType.Initialised;

        return new MemoryHistoryAccessEntry(position, spanId, index % 64 * 8, 1 + index % 9, isRead ? 1 + index % 4 : 1, type);
    }
}
