using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Memory;

public sealed partial class ObjectPoolSnapshot
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Entries.Count);

        foreach (var entry in Entries)
        {
            writer.Write(entry.EntryAddress);

            writer.Write(entry.DataAddress);

            writer.Write(entry.MemoryObjectAddress);

            writer.Write(entry.PagesKb);

            writer.Write((int)entry.ObjectType);

            writer.Write(entry.DatabaseId);

            writer.Write(entry.HobtId);

            writer.Write(entry.ColumnId);

            writer.Write(entry.ObjectId);

            writer.Write(entry.ObjectSize);
        }
    }

    internal static ObjectPoolSnapshot Read(BinaryReader reader)
    {
        var entries = new ObjectPoolEntry[reader.ReadInt32()];

        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = new ObjectPoolEntry(EntryAddress: reader.ReadUInt64(),
                                                 DataAddress: reader.ReadUInt64(),
                                                 MemoryObjectAddress: reader.ReadUInt64(),
                                                 PagesKb: reader.ReadInt64(),
                                                 ObjectType: (ColumnStoreObjectType)reader.ReadInt32(),
                                                 DatabaseId: reader.ReadInt32(),
                                                 HobtId: reader.ReadUInt64(),
                                                 ColumnId: reader.ReadInt32(),
                                                 ObjectId: reader.ReadInt32(),
                                                 ObjectSize: reader.ReadInt64());
        }

        return new ObjectPoolSnapshot(entries);
    }
}
