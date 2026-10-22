using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore
{
    private const int FormatVersion = 8;

    internal static bool IsSupported(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

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
        }

        writer.Write(Writes.Length);

        for (var index = 0; index < Writes.Length; index++)
        {
            var write = Writes[index];

            WritePosition(writer, write.Position);

            writer.Write(GetInstructions(index));

            writer.Write(write.CallNodeIndex);

            writer.Write(write.CallIndex);

            writer.Write(write.Offset);

            writer.Write((byte)write.Type);

            writer.Write((int)write.Length);

            writer.Write(GetBytes(write));
        }

        writer.Write(Iterators.Count);

        foreach (var (callNodeIndex, iterator) in Iterators)
        {
            writer.Write(callNodeIndex);

            writer.Write(iterator);
        }
    }

    internal static MemoryHistoryStore Read(BinaryReader reader)
    {
        if (!IsSupported(reader))
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
                                                   WriteCount: reader.ReadInt32());
        }

        var writes = new MemoryHistoryWrite[reader.ReadInt32()];

        uint[]? highs = null;

        var data = new List<byte>();

        for (var index = 0; index < writes.Length; index++)
        {
            var position = ReadPosition(reader);

            var instructions = reader.ReadUInt64();

            var callNodeIndex = reader.ReadInt32();

            var callIndex = reader.ReadInt32();

            var offset = reader.ReadInt32();

            var type = (MemoryHistoryChangeType)reader.ReadByte();

            var bytes = reader.ReadBytes(reader.ReadInt32());

            if (instructions > uint.MaxValue)
            {
                highs ??= new uint[writes.Length];

                highs[index] = (uint)(instructions >> 32);
            }

            writes[index] = new MemoryHistoryWrite(position,
                                                   (uint)instructions,
                                                   callNodeIndex,
                                                   callIndex,
                                                   offset,
                                                   data.Count,
                                                   (ushort)bytes.Length,
                                                   type);

            data.AddRange(bytes);
        }

        var iterators = new Dictionary<int, ulong>();

        for (var count = reader.ReadInt32(); count > 0; count--)
        {
            iterators[reader.ReadInt32()] = reader.ReadUInt64();
        }

        return new MemoryHistoryStore(blocks,
                                      writes,
                                      highs is null ? HighWordRuns.Empty : HighWordRuns.Build(highs),
                                      [.. data],
                                      iterators);
    }

    private static void WritePosition(BinaryWriter writer, TracePosition position)
    {
        writer.Write(position.Sequence);

        writer.Write(position.Steps);
    }

    private static TracePosition ReadPosition(BinaryReader reader) => new(reader.ReadUInt64(), reader.ReadUInt64());
}
