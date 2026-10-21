using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal static TraceTimelineRow Read(BinaryReader reader, TraceScale scale)
        => new(scale,
               ExportBinary.ReadArray<TracePoint>(reader),
               ExportBinary.ReadArray<TracePoint>(reader),
               ExportBinary.ReadArray<int>(reader),
               ExportBinary.ReadArray<uint>(reader),
               ExportBinary.ReadArray<byte>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Froms);

        ExportBinary.WriteArray(writer, Tos);

        ExportBinary.WriteArray<int>(writer, CallNodeIndexes.AsSpan(Offset, Count));

        ExportBinary.WriteArray<uint>(writer, Calls.AsSpan(Offset, Count));

        ExportBinary.WriteArray<byte>(writer, Flags.AsSpan(Offset, Count));
    }
}
