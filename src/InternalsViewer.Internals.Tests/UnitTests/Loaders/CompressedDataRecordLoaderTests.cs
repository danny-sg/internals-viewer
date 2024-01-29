using InternalsViewer.Internals.Engine.Records;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Internals.Tests.UnitTests.Loaders;

public class CompressedDataRecordLoaderTests
{
    [Theory]
    [InlineData(0x00, false, false, RecordType.Primary, false)]
    [InlineData(01, true, false, RecordType.Primary, false)]
    [InlineData(0x21, true, false, RecordType.Primary, true)]
    [InlineData(0x19, true, false, CompressedRecordType.Index, false)]

    public void Can_Load_Header(byte header, bool isCdRecord, bool containsRowVersioning, CompressedRecordType recordType, bool hasLongDataRegion)
    {
        var record = new CompressedDataRecord(null);

        CompressedDataRecordLoader.ParseHeader(record, new[] { header }, 0);

        Assert.Equal(isCdRecord, record.IsCompressedDataRecord);
        Assert.Equal(containsRowVersioning, record.HasVersioning);
        Assert.Equal(recordType, record.RecordType);
        Assert.Equal(hasLongDataRegion, record.HasLongDataRegion);
    }

    [Fact]
    public void Can_Load_Record()
    {
        var loader = new CompressedDataRecordLoader(null);

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

        Assert.Equal(ColumnDescriptor.OneByteShort, record.ColumnDescriptors[0]);

        Assert.Equal(ColumnDescriptor.Long, record.ColumnDescriptors[1]);

        Assert.Equal(ColumnDescriptor.FiveByteShort, record.ColumnDescriptors[2]);

        Assert.Equal("101", record.Fields[0].Value);
        Assert.Equal("Row 1", record.Fields.First(f=>f.ColumnStructure.ColumnName == "FixedTextField").Value);
    }

    private readonly byte[] data = @"
01090404 28001883 03008118 8b80a31e 00000000
5380a31e 00000000".CleanHex().ToByteArray();

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
    // 
    // 
    // 
    // 
    // 
    // 
    // 
    // 
    // 
    // 
    // 
    // Slot 0 Offset 0x0 Length 0 Length (physical) 0
    // 
    // KeyHashValue = (a2778831b0df)       
    // 
    private readonly byte[] dataWithLongData = @"
2103a216 e5526f77 20310101 000d0054 68697320
69732072 6f772031".CleanHex().ToByteArray();

    private readonly byte[] ciStructure = @"
060f0023 001b0201 09040428 00188303 0081188b
80a31e00 00000053 80a31e00 00000071 00e600e8
00ea00ec 00ee00f0 00f200f4 00f600f8 00fa00fc
00fe0000 01020104 01060108 010a010c 010e0110
01120114 01160118 011a011c 011e0120 01220124
01260128 012a012c 012e0130 01320134 01360138
013a013c 013e0140 01420144 01460148 014a014c
014e0150 01520154 01560158 015a015c 015e0160
01620164 01660168 016a016c 016e0170 01720174
0177017a 017d0180 01830186 0189018c 018f0192
01950198 019b019e 01a101a4 01a701aa 01ad01b0
01b301b6 01b901bc 01bf01c2 01c501c8 01cb01ce
01d101d4 01d801dc 01e001e4 01e801ec 01f001f4
01f80102 7e028002 81028302 84028502 86028802
8a028c02 8d028e02 8f029002 91029202 93029402
95029682 c382c482 c782c882 ca82cb82 e282e782
ec830b83 29834383 5a835b83 60836183 63836483
65836683 6c836d83 70837183 73837483 75837f83
89838c83 8e839583 96839d83 9e83a783 a883a983
b483b983 bd83bf83 c183c283 c383c683 cc83d383
dd83df83 e1f4f480 ba5480e4 43826dd1 830ff083
34148492 6a84f547 8508d485 5ad785f1 a0864c4e
86f11786 fde08702 ca89251e 8a54a28a fc808c06
848d948e 8db7d29c 347e9dd0 b2a5a5ec a613c4a9
1c7abb53 38be1258 c32a4ed1 8e98d489 e8f15b46
fb19c880 8577a880 9863e080 9a129480 a1a34c80
bae05880 fc229880 fee1b881 05ebf881 0c6f38".CleanHex().ToByteArray();
}
