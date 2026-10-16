using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TraceCallRows
{
    public static ulong[] Row(ulong sequence,
                              ulong flags = 0,
                              ulong rcx = 0,
                              ulong rdx = 0,
                              ulong r8 = 0,
                              ulong returnValue = 0,
                              int node = 0,
                              ulong steps = 0,
                              ulong endSequence = ulong.MaxValue,
                              ulong endSteps = 0)
    {
        var row = new ulong[TraceCallColumns.ValueCount];

        row[TraceCallColumns.FromSequenceIndex] = sequence;
        row[TraceCallColumns.FromStepsIndex] = steps;
        row[TraceCallColumns.FlagsIndex] = flags;
        row[TraceCallColumns.IntegerOffset] = rcx;
        row[TraceCallColumns.IntegerOffset + 1] = rdx;
        row[TraceCallColumns.IntegerOffset + 2] = r8;
        row[TraceCallColumns.ReturnValueIndex] = returnValue;
        row[TraceCallColumns.NodeIndex] = (ulong)node;
        row[TraceCallColumns.ToSequenceIndex] = endSequence;
        row[TraceCallColumns.ToStepsIndex] = endSteps;

        return row;
    }

    public static TraceCallStore CallsFor(IReadOnlyList<CallNode> nodes, IEnumerable<CallSpan> spans)
    {
        var builder = new TraceCallStore.Builder();

        foreach (var calls in spans.Where(s => s.Call != uint.MaxValue).GroupBy(s => s.Node))
        {
            ulong[][] rows = [.. calls.OrderBy(s => s.Call)
                                      .Select(s => Row(s.StartSequence,
                                                       flags: s.Thread,
                                                       node: s.Node,
                                                       steps: s.StartSteps,
                                                       endSequence: s.EndSequence,
                                                       endSteps: s.EndSteps))];

            builder.Add(nodes[calls.Key].Address, nodes[calls.Key].Instance, Chunk(rows), rows.Length);
        }

        return builder.Build();
    }

    public static ulong[] Chunk(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TraceCallColumns.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TraceCallColumns.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
