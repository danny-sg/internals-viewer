using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.PublishedEvents;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class PublishedEventHookTests
{
    private const ulong Publisher = 0x7FF8_0080_0000;

    private const ulong Writer = 0x7FF8_0081_0000;

    private const ulong Session = 0x21C_0BB4_0000;

    private const ulong OtherSession = 0x21C_0BB5_0000;

    private const ulong PublishStack = 0x55_47EF_B200;

    private const uint Thread = 9;

    [Fact]
    public void A_Publish_Written_To_A_Session_Is_Recorded_At_The_Publish()
    {
        var (hook, replay) = Create();

        hook.OnCall(Publish(), replay);

        hook.OnCall(Write(Session), replay);

        Assert.Equal(new PublishedEvent(Publisher, Session, 50, 6, 700, 3, Thread), Assert.Single(hook.Build()));
        Assert.Equal([(Thread, PublishStack)], replay.WatchedStacks);
    }

    [Fact]
    public void A_Publish_Written_To_Two_Sessions_Is_Recorded_Twice()
    {
        var (hook, replay) = Create();

        hook.OnCall(Publish(), replay);

        hook.OnCall(Write(Session), replay);

        hook.OnCall(Write(Session), replay);

        hook.OnCall(Write(OtherSession), replay);

        Assert.Equal([Session, OtherSession], hook.Build().Select(e => e.SessionBuffer));
    }

    [Fact]
    public void A_Write_After_The_Publish_Returned_Is_Not_Recorded()
    {
        var (hook, replay) = Create();

        hook.OnCall(Publish(), replay);

        hook.OnStackUnwound(new TraceHookCall { Thread = Thread, StackPointer = PublishStack + 8 });

        hook.OnCall(Write(Session), replay);

        Assert.Empty(hook.Build());
    }

    [Fact]
    public void A_Write_On_Another_Thread_Is_Not_Recorded()
    {
        var (hook, replay) = Create();

        hook.OnCall(Publish(), replay);

        hook.OnCall(Write(Session, thread: 10), replay);

        Assert.Empty(hook.Build());
    }

    private static (PublishedEventHook Hook, FakeTraceReplay Replay) Create()
        => (new PublishedEventHook(new PublishedEventFunctions([new EventPublisher(Publisher, "sql_statement_completed")], [Writer])),
            new FakeTraceReplay());

    private static TraceHookCall Publish()
        => new()
        {
            Function = Publisher,
            Thread = Thread,
            Node = 3,
            Arguments = new ulong[4],
            StackPointer = PublishStack,
            Sequence = 50,
            Steps = 6,
            Instructions = 700
        };

    private static TraceHookCall Write(ulong session, uint thread = Thread)
        => new()
        {
            Function = Writer,
            Thread = thread,
            Arguments = new[] { session, 0ul, 0ul, 0ul },
            StackPointer = PublishStack - 0x200
        };
}
