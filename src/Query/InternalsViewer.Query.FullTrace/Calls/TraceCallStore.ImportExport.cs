using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    private const int FormatVersion = 5;

    internal static bool IsCurrent(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

    internal static TraceCallStore Read(BinaryReader reader)
    {
        var functions = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]>();

        if (!IsCurrent(reader))
        {
            throw new InvalidDataException("The call log was exported by an earlier version and must be re-recorded");
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

        var locators = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable>();

        var uniqueLocators = LocatorTable.ReadLocators(reader);

        var tableCount = reader.ReadInt32();

        for (var index = 0; index < tableCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            locators[key] = LocatorTable.Read(reader, uniqueLocators);
        }

        return new TraceCallStore(functions, locators, uniqueLocators);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        writer.Write(Functions.Count);

        foreach (var ((functionAddress, instanceAddress), chunks) in Functions)
        {
            writer.Write(functionAddress);

            writer.Write(instanceAddress);

            writer.Write(chunks.Length);

            foreach (var chunk in chunks)
            {
                writer.Write(chunk.Calls);

                ExportBinary.WriteArray(writer, chunk.Columns);
            }
        }

        LocatorTable.WriteLocators(writer, UniqueLocators);

        writer.Write(Locators.Count);

        foreach (var ((functionAddress, instanceAddress), table) in Locators)
        {
            writer.Write(functionAddress);

            writer.Write(instanceAddress);

            table.Write(writer);
        }
    }
}
