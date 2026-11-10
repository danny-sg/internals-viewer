using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Mcp.Tests;

internal static class TraceCallRows
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
        var row = new ulong[TraceCallColumnSet.ValueCount];

        row[TraceCallColumnSet.FromSequenceIndex] = sequence;
        row[TraceCallColumnSet.FromStepsIndex] = steps;
        row[TraceCallColumnSet.FlagsIndex] = flags;
        row[TraceCallColumnSet.IntegerOffset] = rcx;
        row[TraceCallColumnSet.IntegerOffset + 1] = rdx;
        row[TraceCallColumnSet.IntegerOffset + 2] = r8;
        row[TraceCallColumnSet.ReturnValueIndex] = returnValue;
        row[TraceCallColumnSet.NodeIndex] = (ulong)callNodeIndex;
        row[TraceCallColumnSet.ToSequenceIndex] = endSequence;
        row[TraceCallColumnSet.ToStepsIndex] = endSteps;

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
        var columns = new ulong[rows.Length * TraceCallColumnSet.ValueCount];

        for (var callIndex = 0; callIndex < rows.Length; callIndex++)
        {
            for (var value = 0; value < TraceCallColumnSet.ValueCount; value++)
            {
                columns[value * rows.Length + callIndex] = rows[callIndex][value];
            }
        }

        return columns;
    }

    public static void Add(this TraceCallStore.Builder builder,
                           ulong functionAddress,
                           ulong instanceAddress,
                           ReadOnlySpan<ulong> columns,
                           int calls)
    {
        var values = new ulong[Math.Max(columns.Length, calls * TraceCallColumnSet.ValueCount)];

        columns.CopyTo(values);

        var handle = GCHandle.Alloc(values, GCHandleType.Pinned);

        try
        {
            builder.Add(functionAddress, instanceAddress, handle.AddrOfPinnedObject(), calls);
        }
        finally
        {
            handle.Free();
        }
    }
}
