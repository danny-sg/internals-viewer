using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BlobHandleParserTests
{
    internal const string OffRowHandle = "4000016C05000000000000000000A000000000010100000000000300040000FF010000000C390000681F0000C19B0000"
                                        + "01000000D03E0000C29B000001000000204E0000C09B00000100000000000000";

    [Fact]
    public void An_Off_Row_Handle_Lists_The_Pages_Of_Its_Value()
    {
        var lob = Assert.IsType<DeferredLobValue>(BlobHandleParser.Parse(Convert.FromHexString(OffRowHandle)));

        Assert.Equal([new DeferredLobLink(8040, new PageAddress(1, 39873), 0),
                      new DeferredLobLink(16080, new PageAddress(1, 39874), 0),
                      new DeferredLobLink(20000, new PageAddress(1, 39872), 0)],
                     lob.Links);

        Assert.Equal("LOB (1:39873), (1:39874), (1:39872)", lob.ToString());
    }

    [Fact]
    public void A_Value_Stored_In_Row_Is_Not_A_Handle()
    {
        var data = Convert.FromHexString(OffRowHandle);

        "Small LOB"u8.CopyTo(data);

        Assert.Null(BlobHandleParser.Parse(data));
    }

    [Fact]
    public void A_Handle_Cut_Short_By_The_Trace_Is_Not_Read()
    {
        Assert.Null(BlobHandleParser.Parse(Convert.FromHexString(OffRowHandle)[..60]));
    }
}
