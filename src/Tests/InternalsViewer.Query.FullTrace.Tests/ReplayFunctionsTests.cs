using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ReplayFunctionsTests
{
    private const ulong Base = 0x7FF8_0000_0000;

    [Fact]
    public void Every_Function_Moves_To_The_Module_Base()
    {
        var relative = new ReplayFunctions([0x10],
                                           [new InstanceMethod(0x50, 1)],
                                           [new LocatorRead(0x60, 2, false, LocatorKind.Page)],
                                           new PublishedEventFunctions([new EventPublisher(0x70, "latch_acquired")], [0x80]),
                                           [new MemoryFunction(0x90, MemoryOperation.Allocate, 1, -1, 1)]);

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.ExcludedFunctions);
        Assert.Equal([new InstanceMethod(Base + 0x50, 1)], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorKind.Page), rebased.LocatorReads[0]);
        Assert.Equal([new EventPublisher(Base + 0x70, "latch_acquired")], rebased.PublishedEvents.Publishers);
        Assert.Equal([Base + 0x80], rebased.PublishedEvents.Writers);
        Assert.Equal(Base + 0x90, Assert.Single(rebased.MemoryFunctions).Address);
    }
}
