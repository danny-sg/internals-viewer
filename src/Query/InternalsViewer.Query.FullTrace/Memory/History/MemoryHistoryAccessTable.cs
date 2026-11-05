using System.Buffers;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed partial class MemoryHistoryAccessTable
{
    private const int ChunkShift = 11;

    private const int ChunkSize = 1 << ChunkShift;

    private const int ChunkMask = ChunkSize - 1;

    private const int EntryBytes = sizeof(ulong) + 5 * sizeof(uint) + sizeof(byte);

    private const int CompressionReserve = 1024;

    private MemoryHistoryAccessTable(byte[][] chunks, int[] dataLengths, int count, int cacheSlots)
    {
        Chunks = chunks;
        DataLengths = dataLengths;
        Count = count;
        Cache = new ChunkBuffer?[Math.Max(cacheSlots, 1)];
    }

    public static MemoryHistoryAccessTable Empty { get; } = new([], [], 0, 1);

    public int Count { get; }

    public long Bytes => Chunks.Sum(c => (long)c.Length) + Chunks.Length * ((long)IntPtr.Size + sizeof(int));

    private byte[][] Chunks { get; }

    private int[] DataLengths { get; }

    private ChunkBuffer?[] Cache { get; }

    private Stack<ChunkBuffer> Windows { get; } = [];

    private Lock Sync { get; } = new();

    private byte[] Columns { get; set; } = [];

    public MemoryHistoryAccessEntry this[int index]
    {
        get
        {
            lock (Sync)
            {
                return GetChunk(index >> ChunkShift).Entries[index & ChunkMask];
            }
        }
    }

    public void CopyBytes(int index, int start, Span<byte> destination)
    {
        lock (Sync)
        {
            GetChunk(index >> ChunkShift).GetBytes(index & ChunkMask).Slice(start, destination.Length).CopyTo(destination);
        }
    }

    public int GetLowerBound(int low, int high, TracePosition position, bool strict)
    {
        lock (Sync)
        {
            while (low < high)
            {
                var middle = low + (high - low) / 2;

                var probe = GetChunk(middle >> ChunkShift).Entries[middle & ChunkMask].Position;

                if (strict ? probe <= position : probe < position)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }
        }

        return low;
    }

    public ChunkBuffer RentWindow()
    {
        lock (Sync)
        {
            return Windows.TryPop(out var window) ? window : new ChunkBuffer();
        }
    }

    public void ReturnWindow(ChunkBuffer window)
    {
        lock (Sync)
        {
            Windows.Push(window);
        }
    }

    public void Read(int index, int first, int end, ChunkBuffer window)
    {
        lock (Sync)
        {
            var chunk = GetChunk(index >> ChunkShift);

            var low = Math.Max(first - chunk.First, 0);

            var high = Math.Min(end - chunk.First, chunk.Count);

            var dataStart = chunk.DataOffsets[low];

            var dataEnd = high < chunk.Count ? chunk.DataOffsets[high] : chunk.DataLength;

            chunk.Entries.AsSpan(low, high - low).CopyTo(window.Entries);

            chunk.Data.AsSpan(dataStart, dataEnd - dataStart).CopyTo(window.ResizeData(dataEnd - dataStart));

            for (var at = low; at < high; at++)
            {
                window.DataOffsets[at - low] = chunk.DataOffsets[at] - dataStart;
            }

            window.First = chunk.First + low;

            window.Count = high - low;
        }
    }

    public static MemoryHistoryAccessTable Build(MemoryHistoryAccessEntry[] entries, byte[] data, int cacheSlots)
    {
        var chunkCount = (entries.Length + ChunkMask) >> ChunkShift;

        var dataStarts = new int[chunkCount + 1];

        var dataIndex = 0;

        for (var index = 0; index < entries.Length; index++)
        {
            if ((index & ChunkMask) == 0)
            {
                dataStarts[index >> ChunkShift] = dataIndex;
            }

            if (entries[index].HasData)
            {
                dataIndex += entries[index].Length;
            }
        }

        if (dataIndex > data.Length)
        {
            throw new ArgumentException("The accesses are longer than their data", nameof(data));
        }

        dataStarts[chunkCount] = dataIndex;

        var chunks = new byte[chunkCount][];

        var dataLengths = new int[chunkCount];

        Parallel.For(0, chunkCount, chunk =>
        {
            var start = chunk << ChunkShift;

            dataLengths[chunk] = dataStarts[chunk + 1] - dataStarts[chunk];

            chunks[chunk] = Compress(entries.AsSpan(start, Math.Min(ChunkSize, entries.Length - start)),
                                     data.AsSpan(dataStarts[chunk], dataLengths[chunk]));
        });

        return new MemoryHistoryAccessTable(chunks, dataLengths, entries.Length, cacheSlots);
    }

    private ChunkBuffer GetChunk(int chunk)
    {
        var cached = Cache[chunk % Cache.Length] ??= new ChunkBuffer();

        if (cached.First != chunk << ChunkShift)
        {
            Decompress(chunk, cached);
        }

        return cached;
    }

    private void Decompress(int chunk, ChunkBuffer buffer)
    {
        var count = Math.Min(ChunkSize, Count - (chunk << ChunkShift));

        if (Columns.Length == 0)
        {
            Columns = new byte[ChunkSize * EntryBytes];
        }

        var bytes = Columns.AsSpan(0, count * EntryBytes);

        buffer.First = -1;

        var data = buffer.ResizeData(DataLengths[chunk]);

        using (var stream = new ZLibStream(new MemoryStream(Chunks[chunk]), CompressionMode.Decompress))
        {
            stream.ReadExactly(bytes);

            stream.ReadExactly(data);
        }

        var columns = new ColumnLayout(bytes, count);

        var sequence = 0ul;

        var spanId = 0;

        var dataIndex = 0;

        for (var index = 0; index < count; index++)
        {
            sequence += (ulong)ZigZagEncoding.Decode(columns.Sequences[index]);

            spanId += (int)ZigZagEncoding.Decode(columns.Spans[index]);

            var entry = new MemoryHistoryAccessEntry(new TracePosition(sequence, columns.Steps[index]),
                                                     spanId,
                                                     (int)columns.Offsets[index],
                                                     (int)columns.Lengths[index],
                                                     (int)columns.Counts[index],
                                                     (MemoryHistoryAccessType)columns.Types[index]);

            buffer.Entries[index] = entry;

            buffer.DataOffsets[index] = dataIndex;

            if (entry.HasData)
            {
                dataIndex += entry.Length;
            }
        }

        buffer.Count = count;

        buffer.First = chunk << ChunkShift;
    }

    private static byte[] Compress(ReadOnlySpan<MemoryHistoryAccessEntry> entries, ReadOnlySpan<byte> data)
    {
        var count = entries.Length;

        var columnLength = count * EntryBytes;

        var length = columnLength + data.Length;

        var bytes = ArrayPool<byte>.Shared.Rent(columnLength);

        var output = ArrayPool<byte>.Shared.Rent(length + (length >> 3) + CompressionReserve);

        try
        {
            WriteColumns(entries, bytes.AsSpan(0, columnLength));

            using var compressed = new MemoryStream(output);

            using (var stream = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                stream.Write(bytes.AsSpan(0, columnLength));

                stream.Write(data);
            }

            return output.AsSpan(0, (int)compressed.Position).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);

            ArrayPool<byte>.Shared.Return(output);
        }
    }

    private static void WriteColumns(ReadOnlySpan<MemoryHistoryAccessEntry> entries, Span<byte> bytes)
    {
        var count = entries.Length;

        var columns = new ColumnLayout(bytes, count);

        var sequence = 0ul;

        var spanId = 0;

        for (var index = 0; index < count; index++)
        {
            var entry = entries[index];

            columns.Sequences[index] = ZigZagEncoding.Encode((long)(entry.Position.Sequence - sequence));

            columns.Steps[index] = (uint)entry.Position.Steps;

            columns.Spans[index] = (uint)ZigZagEncoding.Encode((long)entry.SpanId - spanId);

            columns.Offsets[index] = (uint)entry.Offset;

            columns.Lengths[index] = (uint)entry.Length;

            columns.Counts[index] = (uint)entry.Count;

            columns.Types[index] = (byte)entry.Type;

            sequence = entry.Position.Sequence;

            spanId = entry.SpanId;
        }
    }

    internal sealed class ChunkBuffer
    {
        public MemoryHistoryAccessEntry[] Entries { get; } = new MemoryHistoryAccessEntry[ChunkSize];

        public int[] DataOffsets { get; } = new int[ChunkSize];

        public byte[] Data { get; private set; } = [];

        public int DataLength { get; private set; }

        public int First { get; set; } = -1;

        public int Count { get; set; }

        public bool Contains(int index) => index >= First && index < First + Count;

        public ReadOnlySpan<byte> GetBytes(int index)
            => Entries[index].HasData ? Data.AsSpan(DataOffsets[index], Entries[index].Length) : [];

        public Span<byte> ResizeData(int length)
        {
            if (Data.Length < length)
            {
                Data = new byte[BitOperations.RoundUpToPowerOf2((uint)length)];
            }

            DataLength = length;

            return Data.AsSpan(0, length);
        }
    }

    private readonly ref struct ColumnLayout
    {
        public ColumnLayout(Span<byte> bytes, int count)
        {
            var values = MemoryMarshal.Cast<byte, uint>(bytes.Slice(count * sizeof(ulong), 5 * count * sizeof(uint)));

            Sequences = MemoryMarshal.Cast<byte, ulong>(bytes[..(count * sizeof(ulong))]);
            Steps = values[..count];
            Spans = values.Slice(count, count);
            Offsets = values.Slice(2 * count, count);
            Lengths = values.Slice(3 * count, count);
            Counts = values.Slice(4 * count, count);
            Types = bytes.Slice(count * (EntryBytes - sizeof(byte)), count);
        }

        public Span<ulong> Sequences { get; }

        public Span<uint> Steps { get; }

        public Span<uint> Spans { get; }

        public Span<uint> Offsets { get; }

        public Span<uint> Lengths { get; }

        public Span<uint> Counts { get; }

        public Span<byte> Types { get; }
    }
}
