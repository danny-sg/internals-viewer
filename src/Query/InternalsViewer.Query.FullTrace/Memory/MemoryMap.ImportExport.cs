using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Native;

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

            writer.Write(entry.Owner);

            writer.Write(entry.Until);

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
            entries[index] = new AddressEntry(reader.ReadUInt64(),
                                              reader.ReadUInt64(),
                                              reader.ReadUInt64(),
                                              reader.ReadUInt64(),
                                              reader.ReadInt32(),
                                              reader.ReadUInt32(),
                                              reader.ReadByte());
        }

        return Build(entries, nodeOf);
    }
}
