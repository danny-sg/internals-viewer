using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TraceCallRowHelper
{
    public static ulong[] Row(ulong sequence,
                              ulong flags = 0,
                              ulong rcx = 0,
                              ulong rdx = 0,
                              ulong r8 = 0,
                              ulong returnValue = 0,
                              int callNodeIndex = 0,
                              ulong steps = 0,
                              ulong endSequence = ulong.MaxValue,
                              ulong endSteps = 0)
    {
        var row = new ulong[TraceCallChunk.ValueCount];

        row[TraceCallChunk.FromSequenceIndex] = sequence;
        row[TraceCallChunk.FromStepsIndex] = steps;
        row[TraceCallChunk.FlagsIndex] = flags;
        row[TraceCallChunk.IntegerOffset] = rcx;
        row[TraceCallChunk.IntegerOffset + 1] = rdx;
        row[TraceCallChunk.IntegerOffset + 2] = r8;
        row[TraceCallChunk.ReturnValueIndex] = returnValue;
        row[TraceCallChunk.NodeIndex] = (ulong)callNodeIndex;
        row[TraceCallChunk.ToSequenceIndex] = endSequence;
        row[TraceCallChunk.ToStepsIndex] = endSteps;

        return row;
    }

    public static TraceCallStore CallsFor(IReadOnlyList<CallNode> nodes, IEnumerable<CallSpan> spans)
    {
        var builder = new TraceCallStore.Builder();

        foreach (var calls in spans.Where(s => s.CallIndex != uint.MaxValue).GroupBy(s => s.CallNodeIndex))
        {
            ulong[][] rows = [.. calls.OrderBy(s => s.CallIndex)
                                      .Select(s => Row(s.StartSequence,
                                                       flags: s.Thread,
                                                       callNodeIndex: s.CallNodeIndex,
                                                       steps: s.StartSteps,
                                                       endSequence: s.EndSequence,
                                                       endSteps: s.EndSteps))];

            builder.Add(nodes[calls.Key].FunctionAddress, nodes[calls.Key].InstanceAddress, Chunk(rows), rows.Length);
        }

        return builder.Build();
    }

    public static ulong[] Chunk(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TraceCallChunk.ValueCount];

        for (var callIndex = 0; callIndex < rows.Length; callIndex++)
        {
            for (var value = 0; value < TraceCallChunk.ValueCount; value++)
            {
                columns[value * rows.Length + callIndex] = rows[callIndex][value];
            }
        }

        return columns;
    }
}
