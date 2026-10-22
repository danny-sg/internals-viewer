using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceThreadClock
{
    internal static TraceThreadClock Read(BinaryReader reader)
        => new(ExportBinary.ReadArray<uint>(reader), ExportBinary.ReadArray<ulong>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Sequences);

        ExportBinary.WriteArray(writer, Starts);
    }
}
