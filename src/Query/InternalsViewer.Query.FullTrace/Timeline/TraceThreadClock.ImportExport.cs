using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceThreadClock
{
    internal static TraceThreadClock Read(BinaryReader reader)
    {
        var clock = new TraceThreadClock(ExportBinary.ReadArray<uint>(reader), ExportBinary.ReadArray<ulong>(reader));

        var froms = ExportBinary.ReadArray<TracePosition>(reader);

        var tos = ExportBinary.ReadArray<TracePosition>(reader);

        clock.AddDeadSpans(froms.Zip(tos));

        return clock;
    }

    internal void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Sequences);

        ExportBinary.WriteArray(writer, Starts);

        ExportBinary.WriteArray(writer, DeadFroms);

        ExportBinary.WriteArray(writer, DeadTos);
    }
}
