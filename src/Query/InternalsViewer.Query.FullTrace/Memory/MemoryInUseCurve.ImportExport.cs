using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryInUseCurve
{
    internal static MemoryInUseCurve Read(BinaryReader reader)
        => new(ExportBinary.ReadArray<TracePosition>(reader), ExportBinary.ReadArray<ulong>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Positions);

        ExportBinary.WriteArray(writer, Values);
    }
}
