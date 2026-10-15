using InternalsViewer.Query.FullTrace.Calls;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TraceCallRows
{
    public static ulong[] Row(ulong sequence,
                              ulong flags = 0,
                              ulong rcx = 0,
                              ulong returnValue = 0,
                              int node = 0,
                              ulong id = 0,
                              ulong endId = 0)
    {
        var row = new ulong[TraceArgumentCall.ValueCount];

        row[0] = sequence;
        row[1] = flags;
        row[2] = rcx;
        row[6] = returnValue;
        row[7] = (ulong)node;
        row[8] = id;
        row[9] = endId;

        return row;
    }

    public static ulong[] Chunk(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TraceArgumentCall.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TraceArgumentCall.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
