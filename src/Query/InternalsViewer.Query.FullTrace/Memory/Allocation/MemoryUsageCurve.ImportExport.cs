using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Memory.Allocation;

public sealed partial class MemoryUsageCurve
{
    internal static MemoryUsageCurve Read(BinaryReader reader)
        => new(ExportBinary.ReadArray<TracePosition>(reader), ExportBinary.ReadArray<ulong>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Positions);

        ExportBinary.WriteArray(writer, Values);
    }
}
