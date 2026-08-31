using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Parsers;
using InternalsViewer.Query.Events.Waits;

namespace InternalsViewer.Query.Tests;

/// <summary>
/// Waits stranded when the system-object latches they measure are excluded on the way in
/// </summary>
/// <remarks>
/// Not reachable from the reader's integration tests: those build a bare DatabaseSource with no allocation units, so no
/// event ever resolves to a system object and nothing is ever excluded. The exclusion — and this — only bites in the app,
/// where the allocation metadata is loaded.
/// </remarks>
[Trait("Category", "Unit")]
public class SystemObjectWaitTests
{
    [Fact]
    public void Excluding_A_System_Latch_Strands_Its_Wait()
    {
        var parser = Parser();

        Assert.True(parser.IsExcluded(SystemLatch(latchAddress: 200)));

        // The wait knows only the address it suspended on, so the excluded latch's address is what identifies it.
        Assert.True(parser.IsSystemObjectWait(PageIoWait(waitResource: 200)));
    }

    [Fact]
    public void Wait_On_A_Latch_That_Was_Not_Excluded_Is_Kept()
    {
        var parser = Parser();

        parser.IsExcluded(SystemLatch(latchAddress: 200));

        Assert.False(parser.IsSystemObjectWait(PageIoWait(waitResource: 999)));
    }

    [Fact]
    public void Latch_On_A_User_Object_Is_Not_Excluded_And_Does_Not_Strand_Its_Wait()
    {
        var parser = Parser();

        var userLatch = new LatchEvent
        {
            Name = "latch_suspend_begin",
            LatchAddress = 200,
            AllocationUnit = new AllocationUnit { IsSystem = false },
        };

        Assert.False(parser.IsExcluded(userLatch));
        Assert.False(parser.IsSystemObjectWait(PageIoWait(waitResource: 200)));
    }

    [Fact]
    public void Including_System_Objects_Excludes_Nothing_And_Strands_Nothing()
    {
        // The latches survive, so every wait keeps a suspend to align to and must not be dropped.
        var parser = new EventParser { IncludeSystemObjects = true };

        Assert.False(parser.IsExcluded(SystemLatch(latchAddress: 200)));
        Assert.False(parser.IsSystemObjectWait(PageIoWait(waitResource: 200)));
    }

    [Fact]
    public void Wait_With_No_Resource_Is_Kept()
    {
        // A scheduler yield has no latch behind it and no resource to match on.
        var parser = Parser();

        parser.IsExcluded(SystemLatch(latchAddress: 200));

        var yield = new WaitEvent { Name = "Wait", WaitType = WaitType.SOS_SCHEDULER_YIELD };

        Assert.False(parser.IsSystemObjectWait(yield));
    }

    [Fact]
    public void Non_Wait_Event_Sharing_An_Excluded_Address_Is_Kept()
    {
        var parser = Parser();

        parser.IsExcluded(SystemLatch(latchAddress: 200));

        Assert.False(parser.IsSystemObjectWait(new EngineEvent { Name = "physical_page_read" }));
    }

    private static EventParser Parser() => new();

    private static LatchEvent SystemLatch(ulong latchAddress) => new()
    {
        Name = "latch_suspend_begin",
        LatchClass = LatchClass.BUF,
        LatchMode = LatchMode.SH,
        LatchAddress = latchAddress,
        AllocationUnit = new AllocationUnit { IsSystem = true },
    };

    private static WaitEvent PageIoWait(ulong waitResource) => new()
    {
        Name = "Wait",
        WaitType = WaitType.PAGEIOLATCH_SH,
        WaitResource = waitResource,
    };
}
