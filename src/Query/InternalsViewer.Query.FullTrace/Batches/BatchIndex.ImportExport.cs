namespace InternalsViewer.Query.FullTrace.Batches;

public sealed partial class BatchIndex
{
    internal const int FormatVersion = 2;

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

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
        var first = reader.ReadInt32();

        var version = first < 0 ? -first : 1;

        var count = first < 0 ? reader.ReadInt32() : first;

        var tables = new Dictionary<(ulong Address, ulong Instance), BatchTable>(count);

        for (var index = 0; index < count; index++)
        {
            var address = reader.ReadUInt64();

            var instance = reader.ReadUInt64();

            tables[(address, instance)] = BatchTable.Read(reader, address, instance, version);
        }

        return new BatchIndex(tables);
    }
}
