using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Loaders.Records.Fields;

public class LobOverflowFieldLoaderTests
{
    private const string TestData = "02 00 00 00 01 00 00 00 23 48 00 00 40 1F 00 00 4D 03 00 00 01 00 00 00";

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// DBCC Page output:
    /// 
    ///     Column3 = [BLOB Inline Root] Slot 0 Column 3 Offset 0x1f51 Length 24 Length (physical) 24
    ///     
    ///     Level = 0                           Unused = 0                          UpdateSeq = 1
    ///     TimeStamp = 1210253312              Type = 2                            
    ///     Link 0
    ///     
    ///     Size = 8000                         RowId = (1:845:0)         
    /// </remarks>
    [Fact]
    public void Can_Load_Overflow_Field()
    {
        var data = TestData.ToByteArray();

        var field = LobOverflowFieldLoader.Load(data, 0);

        Assert.Equal(BlobFieldType.RowOverflow, field.PointerType);
        Assert.Equal(0, field.Level);
        Assert.Equal((uint)1210253312, field.Timestamp);
        Assert.Equal(1, field.UpdateSeq);
        Assert.Equal(8000, field.Length);
        Assert.Equal(new RowIdentifier(1, 845, 0), field.Links[0].RowIdentifier);
    }
}