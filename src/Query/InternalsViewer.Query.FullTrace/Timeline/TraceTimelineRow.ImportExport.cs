using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimelineRow
{
    internal static TraceTimelineRow Read(BinaryReader reader)
        => new(ExportBinary.ReadArray<double>(reader),
               ExportBinary.ReadArray<double>(reader),
               ExportBinary.ReadArray<double>(reader),
               ExportBinary.ReadArray<double>(reader),
               ExportBinary.ReadArray<int>(reader),
               ExportBinary.ReadArray<uint>(reader),
               ExportBinary.ReadArray<byte>(reader));

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray<double>(writer, PositionStarts.AsSpan(Offset, Count));

        ExportBinary.WriteArray<double>(writer, PositionEnds.AsSpan(Offset, Count));

        ExportBinary.WriteArray<double>(writer, InstructionStarts.AsSpan(Offset, Count));

        ExportBinary.WriteArray<double>(writer, InstructionEnds.AsSpan(Offset, Count));

        ExportBinary.WriteArray<int>(writer, Nodes.AsSpan(Offset, Count));

        ExportBinary.WriteArray<uint>(writer, Calls.AsSpan(Offset, Count));

        ExportBinary.WriteArray<byte>(writer, Flags.AsSpan(Offset, Count));
    }
}
