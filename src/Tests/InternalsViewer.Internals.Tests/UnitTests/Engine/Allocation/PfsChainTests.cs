using InternalsViewer.Internals.Engine.Allocation;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Allocation;

[Trait("Category", "Unit")]
[Trait("Area", "Allocation")]
public class PfsChainTests
{
    private const byte Allocated = 0x40;
    private const byte Iam = 0x10;
    private const byte Mixed = 0x20;

    private static PfsPage BuildPage(int byteCount, Dictionary<int, byte>? overrides = null)
    {
        var page = new PfsPage { PfsBytes = new byte[byteCount] };

        if (overrides != null)
        {
            foreach (var (index, value) in overrides)
            {
                page.PfsBytes[index] = value;
            }
        }

        return page;
    }

    [Fact]
    public void GetPageStatus_Returns_Byte_For_Page_In_First_Pfs_Page()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, byte> { [3] = Allocated }));

        var result = chain.GetPageStatus(3);

        Assert.Equal(new PfsByte(Allocated), result);
        Assert.True(result.IsAllocated);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_When_Page_Index_Out_Of_Range()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10));

        // Page belongs to the second PFS page (index 1) which does not exist.
        var result = chain.GetPageStatus(PfsPage.PfsInterval + 1);

        Assert.Equal(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_When_Byte_Index_Out_Of_Range()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10));

        var result = chain.GetPageStatus(50);

        Assert.Equal(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Returns_Unknown_For_Empty_Chain()
    {
        var chain = new PfsChain();

        var result = chain.GetPageStatus(0);

        Assert.Equal(PfsByte.Unknown, result);
    }

    [Fact]
    public void GetPageStatus_Indexes_Into_Correct_Pfs_Page()
    {
        var chain = new PfsChain();

        // First PFS page is full size; the target lives at byte 5 of the second PFS page.
        chain.PfsPages.Add(BuildPage(PfsPage.PfsInterval));
        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, byte> { [5] = Iam }));

        var result = chain.GetPageStatus(PfsPage.PfsInterval + 5);

        Assert.Equal(new PfsByte(Iam), result);
        Assert.True(result.IsIam);
    }

    [Fact]
    public void GetPageStatus_Maps_Page_Zero_To_First_Byte()
    {
        var chain = new PfsChain();

        chain.PfsPages.Add(BuildPage(10, new Dictionary<int, byte> { [0] = Mixed }));

        var result = chain.GetPageStatus(0);

        Assert.Equal(new PfsByte(Mixed), result);
        Assert.True(result.IsMixed);
    }
}
