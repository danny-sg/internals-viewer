using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class FullTraceTimelineRow
{
    internal FullTraceTimelineRow(double[] positionStarts,
                                  double[] positionEnds,
                                  double[] instructionStarts,
                                  double[] instructionEnds,
                                  int[] nodes,
                                  uint[] calls,
                                  byte[] flags)
    {
        PositionStarts = positionStarts;
        PositionEnds = positionEnds;
        InstructionStarts = instructionStarts;
        InstructionEnds = instructionEnds;
        Nodes = nodes;
        Calls = calls;
        Flags = flags;
    }

    public int Count => Nodes.Length;

    private double[] PositionStarts { get; }

    private double[] PositionEnds { get; }

    private double[] InstructionStarts { get; }

    private double[] InstructionEnds { get; }

    private int[] Nodes { get; }

    private uint[] Calls { get; }

    private byte[] Flags { get; }

    public ReadOnlySpan<double> Starts(FullTraceTimelineAxis axis)
        => axis == FullTraceTimelineAxis.Position ? PositionStarts : InstructionStarts;

    public ReadOnlySpan<double> Ends(FullTraceTimelineAxis axis)
        => axis == FullTraceTimelineAxis.Position ? PositionEnds : InstructionEnds;

    public int NodeAt(int index) => Nodes[index];

    public int CallAt(int index) => Calls[index] == uint.MaxValue ? -1 : (int)Calls[index];

    public FullTraceSpanFlags FlagsAt(int index) => (FullTraceSpanFlags)Flags[index];

    public FullTraceTimelineSpan Span(FullTraceTimelineAxis axis, int index)
        => new(Starts(axis)[index], Ends(axis)[index], Nodes[index], CallAt(index), FlagsAt(index));

    public FullTraceTimelineRow Where(Func<int, bool> include)
    {
        var kept = new List<int>(Count);

        for (var index = 0; index < Count; index++)
        {
            if (include(Nodes[index]))
            {
                kept.Add(index);
            }
        }

        if (kept.Count == Count)
        {
            return this;
        }

        return new FullTraceTimelineRow([.. kept.Select(i => PositionStarts[i])],
                                        [.. kept.Select(i => PositionEnds[i])],
                                        [.. kept.Select(i => InstructionStarts[i])],
                                        [.. kept.Select(i => InstructionEnds[i])],
                                        [.. kept.Select(i => Nodes[i])],
                                        [.. kept.Select(i => Calls[i])],
                                        [.. kept.Select(i => Flags[i])]);
    }

    public int FirstEndingAfter(FullTraceTimelineAxis axis, double x, int from = 0) => SortedSearch.FirstAfter(Ends(axis), x, from);

    public int FirstStartingFrom(FullTraceTimelineAxis axis, double x, int from) => SortedSearch.FirstAtOrAfter(Starts(axis), x, from);

    public int FirstStartingAfter(FullTraceTimelineAxis axis, double x) => SortedSearch.FirstAfter(Starts(axis), x);

    public int IndexAt(FullTraceTimelineAxis axis, double x, double tolerance)
    {
        var index = FirstEndingAfter(axis, x - tolerance);

        return index < Count && Starts(axis)[index] <= x + tolerance ? index : -1;
    }

    internal static FullTraceTimelineRow Read(BinaryReader reader)
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
