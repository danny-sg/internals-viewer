using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

public class AllocationChainTests
{
    private static AllocationChain BuildChain(short fileId, int setExtent = -1)
    {
        var chain = new AllocationChain { FileId = fileId };

        var page = new AllocationPage();

        if (setExtent >= 0)
        {
            page.AllocationMap[setExtent] = true;
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
    public void Out_Of_Range_Extent_Throws()
    {
        var chain = BuildChain(fileId: 1);

        Assert.Throws<IndexOutOfRangeException>(() =>
            chain.IsExtentAllocated(AllocationPage.AllocationExtentInterval + 1, 1, invert: false));
    }

    [Fact]
    public void Empty_Pages_Throws_For_Any_Extent()
    {
        var chain = new AllocationChain { FileId = 1 };

        Assert.Throws<IndexOutOfRangeException>(() =>
            chain.IsExtentAllocated(0, 1, invert: false));
    }

    [Fact]
    public void SinglePageSlots_Is_Empty()
    {
        var chain = new AllocationChain();

        Assert.Empty(chain.SinglePageSlots);
    }
}
