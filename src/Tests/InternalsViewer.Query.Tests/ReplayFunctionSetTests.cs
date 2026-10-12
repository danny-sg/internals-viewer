using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Markers;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ReplayFunctionSetTests
{
    private const ulong Base = 0x7FF8_0000_0000;

    [Fact]
    public void Every_Function_Moves_To_The_Module_Base()
    {
        var relative = new ReplayFunctionSet([0x10],
                                             [new MemoryFunction(0x20, MemoryOperation.Create, 1, -1, 1) { IsPage = true }],
                                             [new InstanceMethod(0x50, 1)],
                                             [new LocatorRead(0x60, 2, false, LocatorKind.Page)],
                                             [new EventPublisher(0x70, "latch_acquired")],
                                             [0x80]);

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.Excluded);
        Assert.Equal((Base + 0x20, true), (rebased.Memory[0].Address, rebased.Memory[0].IsPage));
        Assert.Equal([new InstanceMethod(Base + 0x50, 1)], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorKind.Page), rebased.Locators[0]);
        Assert.Equal([new EventPublisher(Base + 0x70, "latch_acquired")], rebased.Publishers);
        Assert.Equal([Base + 0x80], rebased.Reserves);
    }
}
