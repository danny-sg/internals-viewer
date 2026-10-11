using InternalsViewer.Query.CallStack.TimeTravel;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
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
                                             new Dictionary<ulong, string> { [0x30] = "lock_acquired" },
                                             [0x40],
                                             [0x50],
                                             [new LocatorRead(0x60, 2, false, LocatorKind.Page)]);

        var rebased = relative.Rebase(Base);

        Assert.Equal([Base + 0x10], rebased.Excluded);
        Assert.Equal((Base + 0x20, true), (rebased.Memory[0].Address, rebased.Memory[0].IsPage));
        Assert.Equal("lock_acquired", rebased.Publishers[Base + 0x30]);
        Assert.Equal([Base + 0x30, Base + 0x40], rebased.Markers);
        Assert.Equal([Base + 0x50], rebased.InstanceMethods);
        Assert.Equal(new LocatorRead(Base + 0x60, 2, false, LocatorKind.Page), rebased.Locators[0]);
    }
}
