using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;

internal sealed class WatchHitStream(uint thread)
{
    private const int FirstSegmentSize = 1 << 12;

    private const int MaximumSegmentSize = 1 << 20;

    private const int MaximumHeaderSize = 40;

    public uint Thread { get; } = thread;

    public bool IsOrdered { get; private set; } = true;

    private List<byte[]> Segments { get; } = [];

    private List<int> SegmentLengths { get; } = [];

    private int Length { get; set; }

    private ulong Sequence { get; set; }

    private ulong Steps { get; set; }

    private ulong NextAddress { get; set; }

    private TracePosition Last { get; set; }

    public void Add(in WatchHit hit, ReadOnlySpan<byte> bytes)
    {
        var needed = MaximumHeaderSize + bytes.Length;

        if (Segments.Count == 0 || Length + needed > Segments[^1].Length)
        {
            if (Segments.Count > 0)
            {
                SegmentLengths.Add(Length);
            }

            Segments.Add(new byte[Math.Max(needed, Math.Min(MaximumSegmentSize, FirstSegmentSize << Segments.Count))]);

            Length = 0;
        }

        var segment = Segments[^1];

        var offset = Length;

        var sequenceDelta = (long)(hit.Sequence - Sequence);

        Write(segment, ref offset, ZigZagEncoding.Encode(sequenceDelta));

        Write(segment, ref offset, sequenceDelta == 0 ? ZigZagEncoding.Encode((long)(hit.Steps - Steps)) : hit.Steps);

        Write(segment, ref offset, ZigZagEncoding.Encode((long)(hit.Address - NextAddress)));

        Write(segment, ref offset, (ulong)bytes.Length);

        bytes.CopyTo(segment.AsSpan(offset));

        Length = offset + bytes.Length;

        Sequence = hit.Sequence;

        Steps = hit.Steps;

        NextAddress = hit.Address + (ulong)bytes.Length;

        var position = hit.Position;

        if (position < Last)
        {
            IsOrdered = false;
        }

        Last = position;
    }

    public Reader GetReader() => new(this);

    private int GetSegmentLength(int segment) => segment < SegmentLengths.Count ? SegmentLengths[segment] : Length;

    private static void Write(byte[] segment, ref int offset, ulong value)
    {
        while (value >= 0x80)
        {
            segment[offset++] = (byte)(value | 0x80);

            value >>= 7;
        }

        segment[offset++] = (byte)value;
    }

    private static ulong Read(byte[] segment, ref int offset)
    {
        var value = 0ul;

        var shift = 0;

        while (true)
        {
            var next = segment[offset++];

            value |= (ulong)(next & 0x7F) << shift;

            if (next < 0x80)
            {
                return value;
            }

            shift += 7;
        }
    }

    public sealed class Reader(WatchHitStream stream)
    {
        private int Segment { get; set; }

        private int Offset { get; set; }

        private ulong Sequence { get; set; }

        private ulong Steps { get; set; }

        private ulong NextAddress { get; set; }

        public (ChunkHit Hit, byte[] Data) Read()
            => TryRead(out var hit, out var data)
                   ? (hit, data)
                   : throw new InvalidOperationException("The watch hit stream has no more hits");

        public bool TryRead(out ChunkHit hit, out byte[] data)
        {
            while (Segment < stream.Segments.Count && Offset >= stream.GetSegmentLength(Segment))
            {
                Segment++;

                Offset = 0;
            }

            if (Segment >= stream.Segments.Count)
            {
                hit = default;

                data = [];

                return false;
            }

            data = stream.Segments[Segment];

            var offset = Offset;

            var sequenceDelta = ZigZagEncoding.Decode(WatchHitStream.Read(data, ref offset));

            var steps = WatchHitStream.Read(data, ref offset);

            Sequence += (ulong)sequenceDelta;

            Steps = sequenceDelta == 0 ? Steps + (ulong)ZigZagEncoding.Decode(steps) : steps;

            var address = NextAddress + (ulong)ZigZagEncoding.Decode(WatchHitStream.Read(data, ref offset));

            var length = (int)WatchHitStream.Read(data, ref offset);

            hit = new ChunkHit(new TracePosition(Sequence, Steps), address, offset, stream.Thread, length);

            NextAddress = address + (ulong)length;

            Offset = offset + length;

            return true;
        }
    }
}
