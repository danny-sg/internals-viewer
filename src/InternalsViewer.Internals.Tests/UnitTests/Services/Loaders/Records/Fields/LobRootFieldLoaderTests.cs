using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Loaders.Records.Fields;

public class LobRootFieldLoaderTests
{
    private const string TestData = "04 00 00 00 01 00 00 00 29 00 00 00 40 1F 00 00 58 03 00 00 01 00 00 00";

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// DBCC Page output:
    /// 
    ///     Column4 = [BLOB Inline Root] Slot 0 Column 4 Offset 0x43 Length 24 Length (physical) 24
    ///     
    ///     Level = 0                           Unused = 0                          UpdateSeq = 1
    ///     TimeStamp = 2686976                 Type = 4                            
    ///     Link 0
    ///     
    ///     Size = 8000                         RowId = (1:856:0)   
    /// </remarks>
    [Fact]
    public void Can_Load_Root_Field()
    {
        var data = TestData.ToByteArray();

        var field = LobRootFieldLoader.Load(data, 0);

        Assert.Equal(BlobFieldType.LobRoot, field.PointerType);
        Assert.Equal(0, field.Level);
        Assert.Equal((uint)2686976, field.Timestamp);
        Assert.Equal(1, field.UpdateSeq);

        Assert.Equal(new RowIdentifier(1, 856, 0), field.Links[0].RowIdentifier);
        Assert.Equal(8000, field.Links[0].Length);
    }
}