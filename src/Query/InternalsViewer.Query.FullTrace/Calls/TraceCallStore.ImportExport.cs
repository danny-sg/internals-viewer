using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed partial class TraceCallStore
{
    private const int FormatVersion = 6;

    internal static bool IsSupported(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

    internal static TraceCallStore Read(BinaryReader reader, TraceNodeMap nodes)
    {
        var functions = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress), Chunk[]>();

        if (!IsSupported(reader))
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

                var stackSlots = reader.ReadInt32();

                chunks[chunk] = new Chunk(calls, stackSlots, ExportBinary.ReadArray<byte>(reader));
            }

            functions[key] = chunks;
        }

        return new TraceCallStore(functions, TraceLocatorSet.Read(reader), nodes);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(-FormatVersion);

        writer.Write(Chunks.Count);

        foreach (var ((functionAddress, instanceAddress), chunks) in Chunks)
        {
            writer.Write(functionAddress);

            writer.Write(instanceAddress);

            writer.Write(chunks.Length);

            foreach (var chunk in chunks)
            {
                writer.Write(chunk.CallCount);

                writer.Write(chunk.StackSlotCount);

                ExportBinary.WriteArray(writer, chunk.Columns);
            }
        }

        Locators.Write(writer);
    }
}
