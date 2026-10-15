using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.FullTrace.RowBuffers;
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
                                           [new LocatorRead(0x60, 2, false, LocatorValueType.Page)],
                                           new PublishedEventFunctions([new EventPublisher(0x70, "latch_acquired")], [0x80]),
                                           [new MemoryFunction(0x90, AllocationOperation.Allocate, 1, -1, 1)],
                                           [new MemoryMapFunction(0xA0, (uint)MemoryMapOperation.Visited)],
                                           new RowBufferFunctions([new RowBufferRead(0xB0, 0)], [new RowBufferLayout(0xC0, 0x80, 0x88)]),
                                           new BatchFunctions([0xD0]));

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.ExcludedFunctions);
        Assert.Equal([new InstanceMethod(Base + 0x50, 1)], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorValueType.Page), rebased.LocatorReads[0]);
        Assert.Equal([new EventPublisher(Base + 0x70, "latch_acquired")], rebased.PublishedEvents.Publishers);
        Assert.Equal([Base + 0x80], rebased.PublishedEvents.Writers);
        Assert.Equal(Base + 0x90, Assert.Single(rebased.MemoryFunctions).Address);
        Assert.Equal(Base + 0xA0, Assert.Single(rebased.MemoryMapFunctions).Address);
        Assert.Equal(Base + 0xB0, Assert.Single(rebased.RowBuffers.Reads).Function);
        Assert.Equal(new RowBufferLayout(Base + 0xC0, 0x80, 0x88), Assert.Single(rebased.RowBuffers.Layouts));
        Assert.Equal([Base + 0xD0], rebased.Batches.Functions);
    }
}
