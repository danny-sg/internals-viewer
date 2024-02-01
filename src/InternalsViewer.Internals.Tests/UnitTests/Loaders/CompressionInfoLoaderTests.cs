using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Loaders;

public class CompressionInfoLoaderTests(ITestOutputHelper testOutputHelper)
{
    private ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public void Can_Load_Ci_Structure()
    {
        var page = new DataPage();

        page.Data = ciStructure;

        var loader = new CompressionInfoLoader(new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutputHelper)));

        var ci = loader.Load(page, 0);
    }

    // CompressionInfo size (in bytes) = 539                                    PageModCount = 15
    // CI Header Flags =  CI_HAS_ANCHOR_RECORD CI_HAS_DICTIONARY                
    // AnchorRecord @0x000000B469DF6067
    // 
    // Record Type = (COMPRESSED) PRIMARY_RECORD                                Record size = 28
    // 
    // CD Array
    // 
    // CD array entry = Column 1 (cluster 0, CD array offset 0): 0x04 (THREE_BYTE_SHORT)
    // CD array entry = Column 2 (cluster 0, CD array offset 0): 0x00 (NULL)    CD array entry = Column 3 (cluster 0, CD array offset 1): 0x04 (THREE_BYTE_SHORT)
    // CD array entry = Column 4 (cluster 0, CD array offset 1): 0x00 (NULL)    CD array entry = Column 5 (cluster 0, CD array offset 2): 0x08 (SEVEN_BYTE_SHORT)
    // CD array entry = Column 6 (cluster 0, CD array offset 2): 0x02 (ONE_BYTE_SHORT)
    // CD array entry = Column 7 (cluster 0, CD array offset 3): 0x00 (NULL)    CD array entry = Column 8 (cluster 0, CD array offset 3): 0x00 (NULL)
    // CD array entry = Column 9 (cluster 0, CD array offset 4): 0x08 (SEVEN_BYTE_SHORT)
    // 
    // Anchor record entry = Column  1, offset   7 length  3
    // Anchor record entry = Column  2, <NULL>
    // Anchor record entry = Column  3, offset  10 length  3
    // Anchor record entry = Column  4, <NULL>
    // Anchor record entry = Column  5, offset  13 length  7
    // Anchor record entry = Column  6, offset  20 length  1
    // Anchor record entry = Column  7, <NULL>
    // Anchor record entry = Column  8, <NULL>
    // Anchor record entry = Column  9, offset  21 length  7
    private readonly byte[] ciStructure = @"060f0023 001b0201 09040428 00188303 0081188b
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
                                            bae05880 fc229880 fee1b881 05ebf881 0c6f38"
                                            .CleanHex()
                                            .ToByteArray();
}