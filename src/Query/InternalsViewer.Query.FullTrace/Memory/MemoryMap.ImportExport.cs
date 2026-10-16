using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Count);

        foreach (var entry in Entries.Values.SelectMany(e => e))
        {
            writer.Write(entry.Address);

            writer.Write(entry.Call);

            writer.Write(entry.FromPosition.Sequence);

            writer.Write(entry.FromPosition.Steps);

            writer.Write(entry.Owner);

            writer.Write(entry.ToPosition.Sequence);

            writer.Write(entry.ToPosition.Steps);

            writer.Write(entry.Node);

            writer.Write(entry.Thread);

            writer.Write((byte)entry.Operation);
        }
    }

    internal static MemoryMap Read(BinaryReader reader, Func<int, CallStackNode?> nodeOf)
    {
        var entries = new AddressEntry[reader.ReadInt32()];

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new AddressEntry(Address: reader.ReadUInt64(),
                                              Frame: 0,
                                              Call: reader.ReadInt32(),
                                              FromPosition: new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                              Owner: reader.ReadUInt64(),
                                              ToPosition: new TracePosition(reader.ReadUInt64(), reader.ReadUInt64()),
                                              Node: reader.ReadInt32(),
                                              Thread: reader.ReadUInt32(),
                                              Operation: (MemoryMapOperation)reader.ReadByte());
        }

        return Build(entries, nodeOf);
    }
}
