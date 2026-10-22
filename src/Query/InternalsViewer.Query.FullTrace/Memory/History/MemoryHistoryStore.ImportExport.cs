using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed partial class MemoryHistoryStore
{
    private const int FormatVersion = 7;

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

        foreach (var write in Writes)
        {
            WritePosition(writer, write.Position);

            writer.Write(write.Instructions);

            writer.Write(write.CallNodeIndex);

            writer.Write(write.CallIndex);

            writer.Write(write.FirstChange);

            writer.Write(write.ChangeCount);
        }

        writer.Write(Iterators.Count);

        foreach (var (callNodeIndex, iterator) in Iterators)
        {
            writer.Write(callNodeIndex);

            writer.Write(iterator);
        }

        writer.Write(Changes.Length);

        foreach (var change in Changes)
        {
            writer.Write(change.Offset);

            writer.Write((byte)change.Type);

            writer.Write(change.Length);

            writer.Write(GetBytes(change));
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

        for (var index = 0; index < writes.Length; index++)
        {
            writes[index] = new MemoryHistoryWrite(Position: ReadPosition(reader),
                                                     Instructions: reader.ReadUInt64(),
                                                     CallNodeIndex: reader.ReadInt32(),
                                                     CallIndex: reader.ReadInt32(),
                                                     FirstChange: reader.ReadInt32(),
                                                     ChangeCount: reader.ReadInt32());
        }

        var iterators = new Dictionary<int, ulong>();

        for (var count = reader.ReadInt32(); count > 0; count--)
        {
            iterators[reader.ReadInt32()] = reader.ReadUInt64();
        }

        var changes = new MemoryHistoryChange[reader.ReadInt32()];

        var data = new List<byte>();

        for (var index = 0; index < changes.Length; index++)
        {
            var offset = reader.ReadInt32();

            var type = (MemoryHistoryChangeType)reader.ReadByte();

            var bytes = reader.ReadBytes(reader.ReadInt32());

            changes[index] = new MemoryHistoryChange(offset, bytes.Length, data.Count, type);

            data.AddRange(bytes);
        }

        return new MemoryHistoryStore(blocks, writes, changes, [.. data], iterators);
    }

    private static void WritePosition(BinaryWriter writer, TracePosition position)
    {
        writer.Write(position.Sequence);

        writer.Write(position.Steps);
    }

    private static TracePosition ReadPosition(BinaryReader reader) => new(reader.ReadUInt64(), reader.ReadUInt64());
}
