using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    private const int FormatVersion = 3;

    internal static bool IsCurrent(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

    internal static TraceCallStore Read(BinaryReader reader)
    {
        var functions = new Dictionary<(ulong Address, ulong Instance), Chunk[]>();

        if (!IsCurrent(reader))
        {
            throw new InvalidDataException("The call log was exported before positions were recorded and must be re-recorded");
        }

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

        return new TraceCallStore(functions, locators);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

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
