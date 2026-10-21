using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    private const int FormatVersion = 1;

    private TraceTimeline(IReadOnlyList<TraceTimelineThread> threads,
                          int[] parents,
                          CallStackNode?[] nodes,
                          TraceScale scale,
                          TracePoint start,
                          TracePoint end)
    {
        Threads = threads;
        Parents = parents;
        Nodes = nodes;
        Scale = scale;
        SpanCount = threads.Sum(t => t.Calls);
        Start = start;
        End = end;
    }

    internal static bool IsCurrent(BinaryReader reader) => reader.ReadInt32() == -FormatVersion;

    internal static TraceTimeline Read(BinaryReader reader, IReadOnlyList<CallStackNode> nodesById)
    {
        if (!IsCurrent(reader))
        {
            throw new InvalidDataException("The trace timeline was exported in an earlier format and must be re-recorded");
        }

        var scale = new TraceScale(reader.ReadDouble());

        var start = TracePointExport.Read(reader);

        var end = TracePointExport.Read(reader);

        var parents = ExportBinary.ReadArray<int>(reader);

        CallStackNode?[] nodes = [.. ExportBinary.ReadArray<int>(reader).Select(id => id < 0 ? null : nodesById[id])];

        var threads = new TraceTimelineThread[reader.ReadInt32()];

        for (var index = 0; index < threads.Length; index++)
        {
            var threadId = reader.ReadUInt32();

            var calls = reader.ReadInt64();

            var rows = new TraceTimelineRow[reader.ReadInt32()];

            for (var depth = 0; depth < rows.Length; depth++)
            {
                rows[depth] = TraceTimelineRow.Read(reader, scale);
            }

            threads[index] = new TraceTimelineThread(threadId, rows, calls);
        }

        return new TraceTimeline(threads, parents, nodes, scale, start, end);
    }

    internal void Write(BinaryWriter writer, Func<CallStackNode, int> idOf)
    {
        writer.Write(-FormatVersion);

        writer.Write(Scale.StepsPerSequence);

        TracePointExport.Write(writer, Start);

        TracePointExport.Write(writer, End);

        ExportBinary.WriteArray(writer, Parents);

        ExportBinary.WriteArray(writer, [.. Nodes.Select(node => node is null ? -1 : idOf(node))]);

        writer.Write(Threads.Count);

        foreach (var thread in Threads)
        {
            writer.Write(thread.ThreadId);

            writer.Write(thread.Calls);

            writer.Write(thread.Rows.Count);

            foreach (var row in thread.Rows)
            {
                row.Write(writer);
            }
        }
    }
}
