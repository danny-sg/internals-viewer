using InternalsViewer.Internals.Engine.Allocation;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

public class IamChainTests
{
    // StartPage PageId 1 gives Extent 0 and a start extent of 0, so an extent maps directly to its
    // bitmap index (relIndex == extent).
    private static IamPage BuildPage(short fileId, params int[] setExtents)
    {
        var page = new IamPage { StartPage = new PageAddress(fileId, 1) };

        foreach (var extent in setExtents)
        {
            var relIndex = extent - page.StartPage.Extent;

            page.AllocationMap[relIndex >> 3] |= (byte)(1 << (relIndex & 7));
        }

        return page;
    }

    private static IamChain BuildChain(params IamPage[] pages)
    {
        var chain = new IamChain();

        chain.Pages.AddRange(pages);
        chain.BuildLookup();

        return chain;
    }

    [Fact]
    public void IsExtentAllocated_Returns_True_For_Allocated_Extent()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.True(chain.IsExtentAllocated(5, fileId: 1, invert: false));
    }

    [Fact]
    public void IsExtentAllocated_Returns_False_For_Unallocated_Extent()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.False(chain.IsExtentAllocated(6, fileId: 1, invert: false));
    }

    [Fact]
    public void IsExtentAllocated_Invert_Negates_Result()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.False(chain.IsExtentAllocated(5, fileId: 1, invert: true));
        Assert.True(chain.IsExtentAllocated(6, fileId: 1, invert: true));
    }

    [Fact]
    public void IsExtentAllocated_Returns_False_For_File_Mismatch()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.False(chain.IsExtentAllocated(5, fileId: 2, invert: false));
    }

    [Fact]
    public void IsExtentAllocated_Invert_True_On_File_Mismatch_Returns_True()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.True(chain.IsExtentAllocated(5, fileId: 2, invert: true));
    }

    [Fact]
    public void IsExtentAllocated_Returns_False_For_Extent_Beyond_Page_Range()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5));

        Assert.False(chain.IsExtentAllocated(200000, fileId: 1, invert: false));
    }

    [Fact]
    public void IsExtentAllocated_Without_BuildLookup_Returns_False()
    {
        var chain = new IamChain();

        chain.Pages.Add(BuildPage(fileId: 1, 5));

        Assert.False(chain.IsExtentAllocated(5, fileId: 1, invert: false));
    }

    [Fact]
    public void IsExtentAllocated_Selects_Page_By_FileId()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 5), BuildPage(fileId: 2, 10));

        Assert.True(chain.IsExtentAllocated(5, fileId: 1, invert: false));
        Assert.True(chain.IsExtentAllocated(10, fileId: 2, invert: false));
        Assert.False(chain.IsExtentAllocated(10, fileId: 1, invert: false));
        Assert.False(chain.IsExtentAllocated(5, fileId: 2, invert: false));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_True_Returns_True_When_Any_Allocated()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 0, 1));

        Assert.True(chain.AnyExtentsAllocated(0, 3, fileId: 1, isInverted: true));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_True_Returns_False_When_None_Allocated()
    {
        var chain = BuildChain(BuildPage(fileId: 1));

        Assert.False(chain.AnyExtentsAllocated(0, 3, fileId: 1, isInverted: true));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_False_Returns_True_When_Any_Unallocated()
    {
        // isInverted: false matches unallocated extents (IsExtentAllocated == isInverted), so extents 2 and 3
        // being unallocated make the result true.
        var chain = BuildChain(BuildPage(fileId: 1, 0, 1));

        Assert.True(chain.AnyExtentsAllocated(0, 3, fileId: 1, isInverted: false));
    }

    [Fact]
    public void AnyExtentsAllocated_Inverted_False_Returns_False_When_All_Allocated()
    {
        var chain = BuildChain(BuildPage(fileId: 1, 0, 1, 2, 3));

        Assert.False(chain.AnyExtentsAllocated(0, 3, fileId: 1, isInverted: false));
    }

    [Fact]
    public void SinglePageSlots_Defaults_To_Empty()
    {
        var chain = new IamChain();

        Assert.Empty(chain.SinglePageSlots);
    }
}
