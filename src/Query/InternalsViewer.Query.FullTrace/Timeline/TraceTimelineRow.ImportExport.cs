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
        ExportBinary.WriteArray(writer, PositionStarts);

        ExportBinary.WriteArray(writer, PositionEnds);

        ExportBinary.WriteArray(writer, InstructionStarts);

        ExportBinary.WriteArray(writer, InstructionEnds);

        ExportBinary.WriteArray(writer, Nodes);

        ExportBinary.WriteArray(writer, Calls);

        ExportBinary.WriteArray(writer, Flags);
    }
}
