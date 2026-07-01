using InternalsViewer.Internals.Engine.Allocation;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

public class PfsChainTests
{
    private static PfsPage BuildPage(int byteCount, Dictionary<int, PfsByte>? overrides = null)
    {
        var page = new PfsPage();

        for (var i = 0; i < byteCount; i++)
        {
            if (overrides != null && overrides.TryGetValue(i, out var value))
            {
                page.PfsBytes.Add(value);
            }
            else
            {
                page.PfsBytes.Add(new PfsByte { Value = 0 });
            }
        }

        return page;
    }

    [Fact]
    public void GetPageStatus_Returns_Byte_For_Page_In_First_Pfs_Page()
    {
        var allocated = new PfsByte { IsAllocated = true };

        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, PfsByte> { [3] = allocated }));

        var result = chain.GetPageStatus(3);

        Assert.Same(allocated, result);
        Assert.True(result.IsAllocated);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_When_Page_Index_Out_Of_Range()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10));

        // Page belongs to the second PFS page (index 1) which does not exist.
        var result = chain.GetPageStatus(PfsPage.PfsInterval + 1);

        Assert.Same(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_When_Byte_Index_Out_Of_Range()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10));

        var result = chain.GetPageStatus(50);

        Assert.Same(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_For_Empty_Chain()
    {
        var chain = new PfsChain();

        var result = chain.GetPageStatus(0);

        Assert.Same(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Indexes_Into_Correct_Pfs_Page()
    {
        var target = new PfsByte { IsIam = true };

        var chain = new PfsChain();

        // First PFS page is full size; the target lives at byte 5 of the second PFS page.
        chain.PfsPages.Add(BuildPage(PfsPage.PfsInterval));
        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, PfsByte> { [5] = target }));

        var result = chain.GetPageStatus(PfsPage.PfsInterval + 5);

        Assert.Same(target, result);
        Assert.True(result.IsIam);
    }

    [Fact]
    public void GetPageStatus_Maps_Page_Zero_To_First_Byte()
    {
        var first = new PfsByte { IsMixed = true };

        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, PfsByte> { [0] = first }));

        var result = chain.GetPageStatus(0);

        Assert.Same(first, result);
    }
}
