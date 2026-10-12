using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Markers;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelMarkerLinkerTests
{
    private const uint Thread = 7;

    private const ulong Session = 0xA000;

    private const ulong OtherSession = 0xB000;

    private static readonly EventPublisher[] Publishers =
    [
        new(0x10, "latch_acquired"),
        new(0x20, "lock_acquired"),
        new(0x30, "wait_info")
    ];

    [Fact]
    public void Link_Pairs_Each_Written_Marker_With_The_Event_Of_The_Same_Name_In_Order()
    {
        var timeline = Timeline();

        var events = Events(100, 200, 300);

        var result = TimeTravelMarkerLinker.Link([Marker(0x10, 10), Marker(0x20, 20), Marker(0x30, 30)],
                                                 Publishers,
                                                 [Raw("latch_acquired", 100), Raw("lock_acquired", 200), Raw("wait_info", 300)],
                                                 events,
                                                 timeline);

        Assert.Equal([100, 200, 300], result.Links.Select(l => l.Event.SequenceId));
        Assert.Equal([10d, 20d, 30d], result.Links.Select(l => l.Position));
    }

    [Fact]
    public void Link_Skips_An_Event_With_No_Marker_And_Keeps_Pairing_The_Rest()
    {
        var result = TimeTravelMarkerLinker.Link([Marker(0x10, 10), Marker(0x20, 20), Marker(0x30, 30)],
                                                 Publishers,
                                                 [Raw("latch_acquired", 100),
                                                  Raw("lock_acquired", 200),
                                                  Raw("lock_acquired", 300),
                                                  Raw("wait_info", 400)],
                                                 Events(100, 200, 300, 400),
                                                 Timeline());

        Assert.Equal([100, 200, 400], result.Links.Select(l => l.Event.SequenceId));
        Assert.Equal([10d, 20d, 30d], result.Links.Select(l => l.Position));
    }

    [Fact]
    public void Link_Only_Uses_Markers_Written_To_The_Busiest_Session()
    {
        var result = TimeTravelMarkerLinker.Link([Marker(0x10, 10), Marker(0x20, 15, OtherSession), Marker(0x20, 20)],
                                                 Publishers,
                                                 [Raw("latch_acquired", 100), Raw("lock_acquired", 200)],
                                                 Events(100, 200),
                                                 Timeline());

        Assert.Equal([10d, 20d], result.Links.Select(l => l.Position));
        Assert.Equal(2, result.Written);
    }

    [Fact]
    public void Link_Gives_Each_Event_The_Call_That_Published_It()
    {
        var node = new CallStackNode();

        var timeline = Timeline();

        timeline.MapNodes([node]);

        var result = TimeTravelMarkerLinker.Link([Marker(0x10, 10)], Publishers, [Raw("latch_acquired", 100)], Events(100), timeline);

        Assert.Same(node, result.Links.Single().Node);
    }

    private static TimeTravelTimeline Timeline()
    {
        var builder = new TimeTravelTimeline.Builder();

        builder.Add([new TimeTravelCallSpan(0, 0, 100, 0, 0, 100, 0, Thread, 0, 1)]);

        return builder.Build([new TimeTravelCallNode(-1, 0, 0x1000, 0, 1)]);
    }

    private static TimeTravelMarker Marker(ulong function, ulong sequence, ulong buffer = Session)
        => new(function, buffer, sequence, 0, sequence, 0, Thread);

    private static RawEvent Raw(string name, int sequence) => new(name, Thread, null, null, sequence);

    private static List<EngineEvent> Events(params int[] sequences)
        => [.. sequences.Select(s => new LatchEvent { SequenceId = s, TimeUs = s })];
}
