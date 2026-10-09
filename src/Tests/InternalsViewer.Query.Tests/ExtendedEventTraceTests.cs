using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Events;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ExtendedEventTraceTests
{
    private const ulong LockPublish = 0x100;

    private const ulong Reserve = 0x200;

    private const ulong WaitPublish = 0x300;

    private const ulong SessionBuffer = 0xB0;

    private const ulong OtherBuffer = 0xC0;

    [Fact]
    public void Events_Written_To_The_Session_Buffer_Are_Compared_With_The_File()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10), 1);
        var lockPublish = tree.AddCall(caller, Frame(LockPublish), 2);
        var lockReserve = tree.AddCall(lockPublish, Frame(Reserve), 2);
        var waitPublish = tree.AddCall(caller, Frame(WaitPublish), 1);
        var waitReserve = tree.AddCall(waitPublish, Frame(Reserve), 1);

        var builder = new TimeTravelCallLog.Builder();

        builder.Add(LockPublish, 0, Chunk(Row(1, 1), Row(3, 1)), 2);
        builder.Add(Reserve, 0, Chunk(Row(2, 2, SessionBuffer), Row(4, 2, SessionBuffer), Row(6, 4, OtherBuffer)), 3);
        builder.Add(WaitPublish, 0, Chunk(Row(5, 3)), 1);

        var log = builder.Build();

        CallStackNode[] nodes = [caller, lockPublish, lockReserve, waitPublish, waitReserve];

        log.MapNodes(nodes);

        var timelineBuilder = new TimeTravelTimeline.Builder();

        timelineBuilder.Add([Span(0, 0, 100, uint.MaxValue),
                             Span(1, 10, 20, 0),
                             Span(2, 12, 13, 0),
                             Span(1, 30, 40, 1),
                             Span(2, 32, 33, 1),
                             Span(3, 50, 60, 0),
                             Span(4, 52, 53, 2)]);

        var timeline = timelineBuilder.Build([new(-1, 0, 0x10, 0, 1),
                                              new(0, 0, LockPublish, 0, 2),
                                              new(1, 0, Reserve, 0, 2),
                                              new(0, 0, WaitPublish, 0, 1),
                                              new(3, 0, Reserve, 0, 1)]);

        timeline.MapNodes(nodes);

        var publishers = new Dictionary<ulong, string> { [LockPublish] = "lock_acquired", [WaitPublish] = "wait_info" };

        RawEvent[] inFile = [new("lock_acquired", 7, 0xAA, 0xBB, null), new("lock_acquired", 7, 0xAA, 0xBB, null)];

        var summary = ExtendedEventTrace.Summarise(timeline, log, publishers, [Reserve], inFile);

        Assert.Equal((3L, 2L, 2L, SessionBuffer), (summary.Published, summary.Written, summary.InFile, summary.SessionBuffer));
        Assert.Empty(summary.Differences);
        Assert.Equal((0xAAul, 7u), Assert.Single(summary.Workers));
    }

    private static CallstackFrame Frame(ulong address) => new() { Module = "sqldk", Rva = (uint)address, Address = address };

    private static TimeTravelCallSpan Span(int node, ulong start, ulong end, uint call)
        => new(1, start, 1, end, start, end, node, 7, call, 1);

    private static ulong[] Row(ulong sequence, int node, ulong receiver = 0)
    {
        var row = new ulong[TimeTravelArgumentCall.ValueCount];

        row[0] = sequence;
        row[2] = receiver;
        row[7] = (ulong)node;

        return row;
    }

    private static ulong[] Chunk(params ulong[][] rows)
    {
        var columns = new ulong[rows.Length * TimeTravelArgumentCall.ValueCount];

        for (var call = 0; call < rows.Length; call++)
        {
            for (var value = 0; value < TimeTravelArgumentCall.ValueCount; value++)
            {
                columns[value * rows.Length + call] = rows[call][value];
            }
        }

        return columns;
    }
}
