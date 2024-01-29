namespace InternalsViewer.Internals.Tests.UnitTests.Services.Loaders.Records;

public class IndexFixedVarRecordLoaderTests
{
    /// <summary>
    /// DBCC Page results:
    /// 
    ///  Row   Level   ChildFileId   ChildPageId   SalesOrderID (key)   SalesOrderDetailID (key)   KeyHashValue   Row Size  
    /// ----- ------- ------------- ------------- -------------------- -------------------------- -------------- ---------- 
    ///    0       2             1          6928   NULL                 NULL                       NULL                 15  
    ///    1       2             1          6929   51828                41987                      NULL                 15  
    ///    2       2             1          6931   65525                91080                      NULL                 15  
    ///    
    /// Slot 0 Offset 0x60 Length 15
    /// 
    /// Record Type = INDEX_RECORD          Record Attributes =                 Record Size = 15
    /// 
    /// Memory Dump @0x00000051219F6060
    /// 
    /// 0000000000000000:   068baa00 00010000 00101b00 000100             .ª............
    /// 
    /// Slot 1 Offset 0x6f Length 15
    /// 
    /// Record Type = INDEX_RECORD          Record Attributes =                 Record Size = 15
    /// 
    /// Memory Dump @0x00000051219F606F
    /// 
    /// 0000000000000000:   0674ca00 0003a400 00111b00 000100             .tÊ...¤........
    /// 
    /// Slot 2 Offset 0x7e Length 15
    /// 
    /// Record Type = INDEX_RECORD          Record Attributes =                 Record Size = 15
    /// 
    /// Memory Dump @0x00000051219F607E
    /// 
    /// 0000000000000000:   06f5ff00 00c86301 00131b00 000100             .õÿ..Èc........
    /// </summary>
    [Theory]
    [InlineData("068baa00 00010000 00101b00 000100", 1, 6928, null, null)]
    [InlineData("0674ca00 0003a400 00111b00 000100", 1, 6929, 51828, 41987)]
    [InlineData("06f5ff00 00c86301 00131b00 000100", 1, 6931, 65525, 91080)]
    public void Can_Load_Clustered_Root_Node_Index_Record(string value, short fileId, int pageId, int? key1, int? key2)
    {

    }
}