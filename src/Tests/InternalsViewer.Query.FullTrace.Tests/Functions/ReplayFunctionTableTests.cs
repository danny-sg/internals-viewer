using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Functions;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;

namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class ReplayFunctionTableTests
{
    private const ulong Base = 0x7FF8_0000_0000;

    [Fact]
    public void Every_Function_Moves_To_The_Module_Base()
    {
        var relative = new ReplayFunctionTable([0x10],
                                           [new InstanceMethod(0x50, 1)],
                                           [new LocatorRead(0x60, 2, false, LocatorValueType.Page)],
                                           [new AllocationFunction(0x90, AllocationOperation.Allocate, 1, -1, 1)],
                                           [new AddressMapFunction(0xA0, AddressMapOperation.Visited)])
        {
            PlanNodeFunctions = Map((0x150, PlanNodeFunctionType.Constructor)),
            PlanObjects = Map((0x158, PlanObjectClass.Row)),
            OutputFunctions = Map((0x110, OutputFunctionType.Send)),
            OutputClasses = Map((0x120, OutputValueClass.OutputRow)),
            BufferPoolFunctions = Map((0x130, BufferPoolFunctionType.Get))
        };

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.ExcludedFunctions);
        Assert.Equal([new InstanceMethod(Base + 0x50, 1)], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorValueType.Page), rebased.LocatorReads[0]);
        Assert.Equal(Base + 0x90, Assert.Single(rebased.AllocationFunctions).Address);
        Assert.Equal(Base + 0xA0, Assert.Single(rebased.AddressMapFunctions).Address);
        Assert.Equal((Base + 0x150, PlanNodeFunctionType.Constructor), Pair(Assert.Single(rebased.PlanNodeFunctions)));
        Assert.Equal((Base + 0x158, PlanObjectClass.Row), Pair(Assert.Single(rebased.PlanObjects)));
        Assert.Equal((Base + 0x110, OutputFunctionType.Send), Pair(Assert.Single(rebased.OutputFunctions)));
        Assert.Equal((Base + 0x120, OutputValueClass.OutputRow), Pair(Assert.Single(rebased.OutputClasses)));
        Assert.Equal((Base + 0x130, BufferPoolFunctionType.Get), Pair(Assert.Single(rebased.BufferPoolFunctions)));
    }

    [Fact]
    public void Vtables_Move_To_The_Module_Base_And_Merge()
    {
        var first = ReplayFunctionTable.Empty with { Vtables = Map((0x200, new[] { "CQScanTableScanNew" })) };

        var second = ReplayFunctionTable.Empty with { Vtables = Map((0x300, new[] { "CQScanRangeNew" })) };

        var merged = ReplayFunctionTable.Merge([first.Rebase(Base), second]);

        Assert.Equal([(0x300ul, "CQScanRangeNew"), (Base + 0x200, "CQScanTableScanNew")],
                     merged.Vtables.Select(v => (v.Key, v.Value[0])).OrderBy(v => v.Key));
    }

    [Fact]
    public void Buffer_Pool_Functions_Move_To_The_Module_Base_And_Merge()
    {
        var first = ReplayFunctionTable.Empty with
        {
            BufferPoolFunctions = Map((0x10, BufferPoolFunctionType.Get), (0x30, BufferPoolFunctionType.PostRead))
        };

        var second = ReplayFunctionTable.Empty with { BufferPoolFunctions = Map((0x20, BufferPoolFunctionType.Get)) };

        var merged = ReplayFunctionTable.Merge([first.Rebase(Base), second]).BufferPoolFunctions;

        (ulong, BufferPoolFunctionType)[] expected =
        [
            (0x20, BufferPoolFunctionType.Get),
            (Base + 0x10, BufferPoolFunctionType.Get),
            (Base + 0x30, BufferPoolFunctionType.PostRead)
        ];

        Assert.Equal(expected, merged.Select(Pair).OrderBy(f => f.Item1));
    }

    private static (ulong, T) Pair<T>(KeyValuePair<ulong, T> entry) => (entry.Key, entry.Value);

    private static AddressTable<T> Map<T>(params (ulong Address, T Value)[] entries)
        => new(entries.ToDictionary(e => e.Address, e => e.Value));
}
