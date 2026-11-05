using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore
{
    private const int FormatVersion = 11;

    private const int InstancesVersion = 10;

    private const int ReadsVersion = 9;

    private const int WritesOnlyVersion = 8;

    internal static bool IsSupported(BinaryReader reader) => ReadVersion(reader) is >= WritesOnlyVersion and <= FormatVersion;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        writer.Write(BlockArray.Length);

        foreach (var block in BlockArray)
        {
            writer.Write(block.Iterator);

            writer.Write(block.Address);

            writer.Write(block.Size);

            writer.Write(block.IsIterator);

            writer.Write(block.IsInline);

            writer.Write(block.Thread);

            writer.Write(block.CallNodeIndex);

            WritePosition(writer, block.FromPosition);

            WritePosition(writer, block.ToPosition);

            writer.Write(block.FromInstructions);

            writer.Write(block.ToInstructions);

            writer.Write(block.FirstWrite);

            writer.Write(block.WriteCount);

            writer.Write(block.IsStack);

            writer.Write(block.FirstInstance);

            writer.Write(block.InstanceCount);
        }

        ExportBinary.WriteArray(writer, Writes);

        ExportBinary.WriteArray(writer, Data);

        writer.Write(Iterators.Count);

        foreach (var (callNodeIndex, iterator) in Iterators)
        {
            writer.Write(callNodeIndex);

            writer.Write(iterator);
        }

        ExportBinary.WriteArray(writer, ReadStarts);

        ExportBinary.WriteArray(writer, Reads);

        writer.Write(Instances.Length);

        foreach (var instance in Instances)
        {
            WritePosition(writer, instance.FromPosition);

            WritePosition(writer, instance.ToPosition);

            writer.Write(instance.FromInstructions);

            writer.Write(instance.ToInstructions);
        }
    }

    internal static MemoryHistoryStore Read(BinaryReader reader, TraceTimeline? timeline)
    {
        var version = ReadVersion(reader);

        if (version is < WritesOnlyVersion or > FormatVersion)
        {
            throw new InvalidDataException("The memory history was exported by an unsupported version and must be re-recorded");
        }

        var blocks = new MemoryHistoryBlock[reader.ReadInt32()];

        for (var index = 0; index < blocks.Length; index++)
        {
            blocks[index] = new MemoryHistoryBlock(Iterator: reader.ReadUInt64(),
                                                   Address: reader.ReadUInt64(),
                                                   Size: reader.ReadInt32(),
                                                   IsIterator: reader.ReadBoolean(),
                                                   IsInline: reader.ReadBoolean(),
                                                   Thread: reader.ReadUInt32(),
                                                   CallNodeIndex: reader.ReadInt32(),
                                                   FromPosition: ReadPosition(reader),
                                                   ToPosition: ReadPosition(reader),
                                                   FromInstructions: reader.ReadUInt64(),
                                                   ToInstructions: reader.ReadUInt64(),
                                                   FirstWrite: reader.ReadInt32(),
                                                   WriteCount: reader.ReadInt32())
            {
                IsStack = version >= InstancesVersion && reader.ReadBoolean(),
                FirstInstance = version >= InstancesVersion ? reader.ReadInt32() : 0,
                InstanceCount = version >= InstancesVersion ? reader.ReadInt32() : 0
            };
        }

        if (version < FormatVersion)
        {
            return ReadCalls(reader, version, blocks, new SpanResolver(timeline));
        }

        var writes = ExportBinary.ReadArray<MemoryHistoryWriteEntry>(reader);

        var data = ExportBinary.ReadArray<byte>(reader);

        var iterators = ReadIterators(reader);

        var readStarts = ExportBinary.ReadArray<int>(reader);

        var reads = ExportBinary.ReadArray<MemoryHistoryReadEntry>(reader);

        return new MemoryHistoryStore(blocks, writes, data, iterators, timeline, reads, readStarts, ReadInstances(reader));
    }

    private static int ReadVersion(BinaryReader reader) => -reader.ReadInt32();

    private static MemoryHistoryStore ReadCalls(BinaryReader reader, int version, MemoryHistoryBlock[] blocks, SpanResolver spans)
    {
        var writes = new MemoryHistoryWriteEntry[reader.ReadInt32()];

        var data = new List<byte>();

        for (var index = 0; index < writes.Length; index++)
        {
            var position = ReadPosition(reader);

            reader.ReadUInt64();

            var spanId = spans.Get(reader.ReadInt32(), reader.ReadInt32(), position);

            var offset = reader.ReadInt32();

            var type = (MemoryHistoryChangeType)reader.ReadByte();

            var bytes = reader.ReadBytes(reader.ReadInt32());

            writes[index] = new MemoryHistoryWriteEntry(position, spanId, offset, (ushort)bytes.Length, type);

            data.AddRange(bytes);
        }

        var iterators = ReadIterators(reader);

        var (reads, readStarts) = version >= ReadsVersion ? ReadCallReads(reader, spans) : ([], []);

        var instances = version >= InstancesVersion ? ReadInstances(reader) : [];

        return new MemoryHistoryStore(blocks, writes, [.. data], iterators, spans.Timeline, reads, readStarts, instances);
    }

    private static Dictionary<int, ulong> ReadIterators(BinaryReader reader)
    {
        var iterators = new Dictionary<int, ulong>();

        for (var count = reader.ReadInt32(); count > 0; count--)
        {
            iterators[reader.ReadInt32()] = reader.ReadUInt64();
        }

        return iterators;
    }

    private static (MemoryHistoryReadEntry[] Reads, int[] Starts) ReadCallReads(BinaryReader reader, SpanResolver spans)
    {
        var starts = new int[reader.ReadInt32()];

        for (var index = 0; index < starts.Length; index++)
        {
            starts[index] = reader.ReadInt32();
        }

        var reads = new MemoryHistoryReadEntry[reader.ReadInt32()];

        for (var index = 0; index < reads.Length; index++)
        {
            var position = ReadPosition(reader);

            reader.ReadUInt64();

            reads[index] = new MemoryHistoryReadEntry(position,
                                                      SpanId: spans.Get(reader.ReadInt32(), reader.ReadInt32(), position),
                                                      Offset: reader.ReadInt32(),
                                                      Length: reader.ReadInt32(),
                                                      Count: reader.ReadInt32());
        }

        return (reads, starts);
    }

    private static MemoryHistoryInstance[] ReadInstances(BinaryReader reader)
    {
        var instances = new MemoryHistoryInstance[reader.ReadInt32()];

        for (var index = 0; index < instances.Length; index++)
        {
            instances[index] = new MemoryHistoryInstance(FromPosition: ReadPosition(reader),
                                                         ToPosition: ReadPosition(reader),
                                                         FromInstructions: reader.ReadUInt64(),
                                                         ToInstructions: reader.ReadUInt64());
        }

        return instances;
    }

    private static void WritePosition(BinaryWriter writer, TracePosition position)
    {
        writer.Write(position.Sequence);

        writer.Write(position.Steps);
    }

    private static TracePosition ReadPosition(BinaryReader reader) => new(reader.ReadUInt64(), reader.ReadUInt64());

    private sealed class SpanResolver(TraceTimeline? timeline)
    {
        public TraceTimeline? Timeline => timeline;

        private Dictionary<(int CallNodeIndex, int CallIndex), int> Calls { get; } = [];

        public int Get(int callNodeIndex, int callIndex, TracePosition position)
        {
            if (timeline is null)
            {
                return -1;
            }

            if (Calls.TryGetValue((callNodeIndex, callIndex), out var spanId)
                && timeline.IsSpanAt(spanId, callNodeIndex, callIndex, position))
            {
                return spanId;
            }

            spanId = timeline.FindSpanId(callNodeIndex, callIndex, position);

            if (spanId >= 0)
            {
                Calls[(callNodeIndex, callIndex)] = spanId;
            }

            return spanId;
        }
    }
}
