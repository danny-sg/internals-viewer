using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.CallLog;

public sealed partial class TraceCallLog
{
    internal static TraceCallLog Read(BinaryReader reader)
    {
        var functions = new Dictionary<(ulong Address, ulong Instance), Chunk[]>();

        var functionCount = reader.ReadInt32();

        for (var index = 0; index < functionCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            var chunks = new Chunk[reader.ReadInt32()];

            for (var chunk = 0; chunk < chunks.Length; chunk++)
            {
                var calls = reader.ReadInt32();

                chunks[chunk] = new Chunk(calls, ExportBinary.ReadArray<byte>(reader));
            }

            functions[key] = chunks;
        }

        var locators = new Dictionary<(ulong Address, ulong Instance), LocatorTable>();

        var tableCount = reader.ReadInt32();

        for (var index = 0; index < tableCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            locators[key] = LocatorTable.Read(reader);
        }

        return new TraceCallLog(functions, locators);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Functions.Count);

        foreach (var ((address, instance), chunks) in Functions)
        {
            writer.Write(address);

            writer.Write(instance);

            writer.Write(chunks.Length);

            foreach (var chunk in chunks)
            {
                writer.Write(chunk.Calls);

                ExportBinary.WriteArray(writer, chunk.Columns);
            }
        }

        writer.Write(Locators.Count);

        foreach (var ((address, instance), table) in Locators)
        {
            writer.Write(address);

            writer.Write(instance);

            table.Write(writer);
        }
    }
}
