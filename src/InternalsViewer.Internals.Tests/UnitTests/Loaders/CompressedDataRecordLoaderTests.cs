using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Loaders;

public class CompressedDataRecordLoaderTests(ITestOutputHelper testOutputHelper)
{
    private ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Theory]
    [InlineData(0x00, false, false, RecordType.Primary, false)]
    [InlineData(01, true, false, RecordType.Primary, false)]
    [InlineData(0x21, true, false, RecordType.Primary, true)]
    [InlineData(0x19, true, false, CompressedRecordType.Index, false)]
    public void Can_Load_Header(byte header, 
                                bool isCdRecord, 
                                bool containsRowVersioning, 
                                CompressedRecordType recordType, 
                                bool hasLongDataRegion)
    {
        var record = new CompressedDataRecord(null!);

        var loader = new CompressedDataRecordLoader(TestLogger.GetLogger<CompressedDataRecordLoader>(TestOutputHelper));

        loader.LoadHeader(record, new[] { header }, 0);

        Assert.Equal(isCdRecord, record.IsCompressedDataRecord);
        Assert.Equal(containsRowVersioning, record.HasVersioning);
        Assert.Equal(recordType, record.RecordType);
        Assert.Equal(hasLongDataRegion, record.HasLongDataRegion);
    }

    [Fact]
    public void Can_Load_Record()
    {
        var loader = new CompressedDataRecordLoader(TestLogger.GetLogger<CompressedDataRecordLoader>(TestOutputHelper));

        var page = new DataPage();

        page.Data = dataWithLongData;

        var tableStructure = new TableStructure(0);

        tableStructure.Columns = new List<ColumnStructure>
        {
            new() { ColumnId = 1, ColumnName = "NumberField", DataType = System.Data.SqlDbType.Int },
            new() { ColumnId = 2, ColumnName = "TextField" , DataType = System.Data.SqlDbType.VarChar },
            new() { ColumnId = 3, ColumnName = "FixedTextField", DataType = System.Data.SqlDbType.Char },
        };

        var record = loader.Load(page, 0, tableStructure);

        Assert.True(record.IsCompressedDataRecord);

        Assert.True(record.HasLongDataRegion);

        Assert.Equal(ColumnDescriptorFlag.OneByteShort, record.ColumnDescriptors[0].Value);

        Assert.Equal(ColumnDescriptorFlag.Long, record.ColumnDescriptors[1].Value);

        Assert.Equal(ColumnDescriptorFlag.FiveByteShort, record.ColumnDescriptors[2].Value);

        Assert.Equal("101", record.Fields[0].Value);
        Assert.Equal("Row 1", record.Fields.First(f => f.ColumnStructure.ColumnName == "FixedTextField").Value);
        Assert.Equal("This is row 1", record.Fields.First(f => f.ColumnStructure.ColumnName == "TextField").Value);
    }

    private readonly byte[] data = @"01090404 28001883 03008118 8b80a31e 00000000
                                     5380a31e 00000000"
                                        .CleanHex()
                                        .ToByteArray();

    // Record Type = (COMPRESSED) PRIMARY_RECORD                                Record attributes =  LONG DATA REGION
    // Record size = 28                    
    // CD Array
    // 
    // CD array entry = Column 1 (cluster 0, CD array offset 0): 0x02 (ONE_BYTE_SHORT)
    // CD array entry = Column 2 (cluster 0, CD array offset 0): 0x0a (LONG)
    // CD array entry = Column 3 (cluster 0, CD array offset 1): 0x06 (FIVE_BYTE_SHORT)
    // 
    // Record Memory Dump
    // 
    // 000000B4723F6060:   2103a216 e5526f77 20310101 000d0054 68697320  !.¢.åRow 1.....This 
    // 000000B4723F6074:   69732072 6f772031                             is row 1
    // 
    // Slot 0 Column 1 Offset 0x4 Length 4 Length (physical) 1
    // 
    // NumberField = 101                   
    // 
    // Slot 0 Column 2 Offset 0xf Length 13 Length (physical) 13
    // 
    // TextField = This is row 1           
    // 
    // Slot 0 Column 3 Offset 0x5 Length 2000 Length (physical) 5
    // 
    // FixedTextField = Row 1                                                                                                                                                                     
    // (snip)
    // Slot 0 Offset 0x0 Length 0 Length (physical) 0
    // 
    // KeyHashValue = (a2778831b0df)       
    private readonly byte[] dataWithLongData = @"2103a216 e5526f77 20310101 000d0054 68697320
                                                 69732072 6f772031"
                                                    .CleanHex().ToByteArray();
}
