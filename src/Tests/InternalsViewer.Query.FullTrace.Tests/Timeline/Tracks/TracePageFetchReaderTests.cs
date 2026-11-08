using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TracePageFetchReaderTests
{
    [Theory]
    [InlineData(1, PageType.Pfs)]
    [InlineData(8088, PageType.Pfs)]
    [InlineData(16176, PageType.Pfs)]
    [InlineData(0, PageType.None)]
    [InlineData(2, PageType.None)]
    [InlineData(8089, PageType.None)]
    public void A_Page_Not_In_The_Buffer_Pool_Is_A_PFS_Page_By_Its_Position_In_The_File(int pageId, PageType expected)
    {
        var access = new TracePageAccess(default, new PageAddress(1, pageId), 5, null, 0, 0);

        Assert.Equal(expected, TracePageFetchReader.GetPageType(access, TraceBufferPool.Empty));
    }

    [Fact]
    public void A_Page_In_The_Buffer_Pool_Takes_Its_Type_From_Its_Header()
    {
        var page = new PageAddress(1, 154);

        var builder = new TraceBufferPool.Builder();

        builder.AddLayout(page, 5, [0x60, 0x8C], PageType.Iam);

        var access = new TracePageAccess(default, page, 5, null, 0, 0);

        Assert.Equal(PageType.Iam, TracePageFetchReader.GetPageType(access, builder.Build()));
    }
}
