using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    private TraceTimeline(IReadOnlyList<TraceTimelineThread> threads,
                          int[] parents,
                          CallStackNode?[] nodes,
                          double stepsPerSequence,
                          (double PositionStart, double PositionEnd, double InstructionStart, double InstructionEnd) bounds,
                          MemoryIndex memory)
    {
        Threads = threads;
        Parents = parents;
        Nodes = nodes;
        StepsPerSequence = stepsPerSequence;
        SpanCount = threads.Sum(t => t.Calls);
        PositionStart = bounds.PositionStart;
        PositionEnd = bounds.PositionEnd;
        InstructionStart = bounds.InstructionStart;
        InstructionEnd = bounds.InstructionEnd;
        Memory = memory;
    }

    internal static TraceTimeline Read(BinaryReader reader, IReadOnlyList<CallStackNode> nodesById)
    {
        var stepsPerSequence = reader.ReadDouble();

        var bounds = (reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());

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
                rows[depth] = TraceTimelineRow.Read(reader);
            }

            threads[index] = new TraceTimelineThread(threadId, rows, calls);
        }

        var memory = MemoryIndex.Read(reader);

        return new TraceTimeline(threads, parents, nodes, stepsPerSequence, bounds, memory);
    }

    internal void Write(BinaryWriter writer, Func<CallStackNode, int> idOf)
    {
        writer.Write(StepsPerSequence);

        writer.Write(PositionStart);

        writer.Write(PositionEnd);

        writer.Write(InstructionStart);

        writer.Write(InstructionEnd);

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

        Memory.Write(writer);
    }

    internal void WriteEventLinks(BinaryWriter writer, Func<CallStackNode, int> idOf)
    {
        writer.Write(EventLinks.Count);

        foreach (var link in EventLinks)
        {
            writer.Write(link.Event.SequenceId);

            writer.Write(link.Node is { } node ? idOf(node) : -1);

            writer.Write(link.ThreadId);

            writer.Write(link.Position);

            writer.Write(link.Instructions);
        }
    }

    internal static List<TraceEventLink> ReadEventLinks(BinaryReader reader,
                                                        IReadOnlyList<CallStackNode> nodesById,
                                                        IEnumerable<EngineEvent> events)
    {
        var bySequence = events.BySequence();

        var count = reader.ReadInt32();

        var links = new List<TraceEventLink>(count);

        for (var index = 0; index < count; index++)
        {
            var sequence = reader.ReadInt32();

            var node = reader.ReadInt32();

            var thread = reader.ReadUInt32();

            var position = reader.ReadDouble();

            var instructions = reader.ReadDouble();

            if (bySequence.TryGetValue(sequence, out var engineEvent))
            {
                links.Add(new TraceEventLink(engineEvent,
                                             node >= 0 && node < nodesById.Count ? nodesById[node] : null,
                                             thread,
                                             position,
                                             instructions));
            }
        }

        return links;
    }
}
