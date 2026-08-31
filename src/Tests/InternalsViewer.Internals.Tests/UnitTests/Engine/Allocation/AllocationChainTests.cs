using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

[Trait("Category", "Unit")]
[Trait("Area", "Allocation")]
public class AllocationChainTests
{
    private static AllocationChain BuildChain(short fileId, int setExtent = -1)
    {
        var chain = new AllocationChain { FileId = fileId };

        var page = new AllocationPage();

        if (setExtent >= 0)
        {
            page.AllocationMap[setExtent >> 3] |= (byte)(1 << (setExtent & 7));
        }

        chain.Pages.Add(page);

        return chain;
    }

    [Fact]
    public void Unallocated_Extent_Returns_False()
    {
        var chain = BuildChain(fileId: 1);

        Assert.False(chain.IsExtentAllocated(0, 1, invert: false));
    }

    [Fact]
    public void Allocated_Extent_Returns_True()
    {
        var chain = BuildChain(fileId: 1, setExtent: 5);

        Assert.True(chain.IsExtentAllocated(5, 1, invert: false));
    }

    [Fact]
    public void Invert_False_On_Allocated_Returns_True()
    {
        var chain = BuildChain(fileId: 1, setExtent: 10);

        Assert.True(chain.IsExtentAllocated(10, 1, invert: false));
    }

    [Fact]
    public void Invert_True_On_Allocated_Returns_False()
    {
        var chain = BuildChain(fileId: 1, setExtent: 10);

        Assert.False(chain.IsExtentAllocated(10, 1, invert: true));
    }

    [Fact]
    public void Invert_True_On_Unallocated_Returns_True()
    {
        var chain = BuildChain(fileId: 1);

        Assert.True(chain.IsExtentAllocated(0, 1, invert: true));
    }

    [Fact]
    public void Wrong_FileId_Returns_False_Even_If_Extent_Allocated()
    {
        var chain = BuildChain(fileId: 1, setExtent: 5);

        Assert.False(chain.IsExtentAllocated(5, fileId: 2, invert: false));
    }

    [Fact]
    public void Out_Of_Range_Extent_Returns_False()
    {
        var chain = BuildChain(fileId: 1);

        Assert.False(chain.IsExtentAllocated(AllocationPage.AllocationExtentInterval + 1, 1, invert: false));
    }

    [Fact]
    public void Empty_Pages_Return_False_For_Any_Extent()
    {
        var chain = new AllocationChain { FileId = 1 };

        Assert.False(chain.IsExtentAllocated(0, 1, invert: false));
    }

    [Fact]
    public void An_Allocated_Extent_Does_Not_Allocate_Its_Byte_Neighbours()
    {
        var chain = BuildChain(fileId: 1, setExtent: 5);

        Assert.False(chain.IsExtentAllocated(4, 1, invert: false));

        Assert.False(chain.IsExtentAllocated(6, 1, invert: false));

        Assert.False(chain.IsExtentAllocated(40, 1, invert: false));
    }

    [Fact]
    public void An_Extent_Past_The_First_Page_Reads_From_The_Second()
    {
        var chain = BuildChain(fileId: 1);

        var second = new AllocationPage();

        second.AllocationMap[0] |= 1;

        chain.Pages.Add(second);

        Assert.True(chain.IsExtentAllocated(AllocationPage.AllocationExtentInterval, 1, invert: false));
    }

    [Fact]
    public void SinglePageSlots_Is_Empty()
    {
        var chain = new AllocationChain();

        Assert.Empty(chain.SinglePageSlots);
    }
}
