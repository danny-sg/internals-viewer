using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Loaders.Records.Fields;

public class LobPointerFieldLoaderTests
{
    private const string TestData = "00 00 35 F2 00 00 00 00 10 23 00 00 01 00 01 00";

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// DBCC Page output:
    /// 
    ///     Col3 = [Textpointer] Slot 0 Column 3 Offset 0x15 Length 16 Length (physical) 16
    ///     
    ///     TextTimeStamp = 4063559680          RowId = (1:8976:1)  
    /// 
    /// </remarks>
    [Fact]
    public void Can_Load_Pointer_Field()
    {
        var data = TestData.ToByteArray();

        var field = LobPointerFieldLoader.Load(data, 0);

        Assert.Equal(BlobFieldType.LobPointer, field.PointerType);
        Assert.Equal(4063559680, field.Timestamp);
        Assert.Equal(new RowIdentifier(1, 8976, 1), field.Links[0].RowIdentifier);
    }
}