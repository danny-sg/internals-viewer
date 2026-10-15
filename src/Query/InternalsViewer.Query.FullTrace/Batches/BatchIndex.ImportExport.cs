namespace InternalsViewer.Query.FullTrace.Batches;

public sealed partial class BatchIndex
{
    internal void Write(BinaryWriter writer)
    {
        writer.Write(Tables.Count);

        foreach (var ((address, instance), table) in Tables)
        {
            writer.Write(address);

            writer.Write(instance);

            table.Write(writer);
        }
    }

    internal static BatchIndex Read(BinaryReader reader)
    {
        var count = reader.ReadInt32();

        var tables = new Dictionary<(ulong Address, ulong Instance), BatchTable>(count);

        for (var index = 0; index < count; index++)
        {
            var address = reader.ReadUInt64();

            var instance = reader.ReadUInt64();

            tables[(address, instance)] = BatchTable.Read(reader, address, instance);
        }

        return new BatchIndex(tables);
    }
}
