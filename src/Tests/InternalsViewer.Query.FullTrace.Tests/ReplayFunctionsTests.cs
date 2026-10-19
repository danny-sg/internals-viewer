using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.PublishedEvents;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.RowSources.Enums;

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
                                           [new MemoryMapFunction(0xA0, MemoryMapOperation.Visited)],
                                           new RowSourceFunctions(Map(0x150, RowSourceFunctionType.Creator), Map(0x158, PlanObjectClass.Row)),
                                           new OutputFunctions(Map(0x110, OutputFunctionType.Send),
                                                               Map(0x120, OutputValueClass.OutputRow)));

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.ExcludedFunctions);
        Assert.Equal([new InstanceMethod(Base + 0x50, 1)], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorValueType.Page), rebased.LocatorReads[0]);
        Assert.Equal([new EventPublisher(Base + 0x70, "latch_acquired")], rebased.PublishedEvents.Publishers);
        Assert.Equal([Base + 0x80], rebased.PublishedEvents.Writers);
        Assert.Equal(Base + 0x90, Assert.Single(rebased.MemoryFunctions).Address);
        Assert.Equal(Base + 0xA0, Assert.Single(rebased.MemoryMapFunctions).Address);
        Assert.Equal((Base + 0x150, RowSourceFunctionType.Creator), Pair(Assert.Single(rebased.RowSources.Functions)));
        Assert.Equal((Base + 0x158, PlanObjectClass.Row), Pair(Assert.Single(rebased.RowSources.PlanObjects)));
        Assert.Equal((Base + 0x110, OutputFunctionType.Send), Pair(Assert.Single(rebased.Output.Functions)));
        Assert.Equal((Base + 0x120, OutputValueClass.OutputRow), Pair(Assert.Single(rebased.Output.Classes)));
    }

    private static (ulong, T) Pair<T>(KeyValuePair<ulong, T> entry) => (entry.Key, entry.Value);

    private static Dictionary<ulong, T> Map<T>(ulong address, T value) => new() { [address] = value };
}
