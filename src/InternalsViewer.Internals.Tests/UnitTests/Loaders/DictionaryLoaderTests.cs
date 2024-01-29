using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Loaders.Compression;

namespace InternalsViewer.Internals.Tests.UnitTests.Loaders;

public class DictionaryLoaderTests(ITestOutputHelper testOutputHelper)
{
    private ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public void Can_Load_Dictionary()
    {
        var page = new DataPage();

        page.Data = dictionary;

        var result = DictionaryLoader.Load(page.Data, 0);

        TestOutputHelper.WriteLine(result.ToString());

        Assert.Equal(228, result.DictionaryEntries[0].Offset);
        Assert.Equal(230, result.DictionaryEntries[1].Offset);
        Assert.Equal(232, result.DictionaryEntries[2].Offset);
        Assert.Equal(234, result.DictionaryEntries[3].Offset);
        Assert.Equal(236, result.DictionaryEntries[4].Offset);
        Assert.Equal(238, result.DictionaryEntries[5].Offset);
        Assert.Equal(240, result.DictionaryEntries[6].Offset);
        Assert.Equal(242, result.DictionaryEntries[7].Offset);
        Assert.Equal(244, result.DictionaryEntries[8].Offset);
        Assert.Equal(246, result.DictionaryEntries[9].Offset);
        Assert.Equal(248, result.DictionaryEntries[10].Offset);
        Assert.Equal(250, result.DictionaryEntries[11].Offset);
        Assert.Equal(252, result.DictionaryEntries[12].Offset);
        Assert.Equal(254, result.DictionaryEntries[13].Offset);
        Assert.Equal(256, result.DictionaryEntries[14].Offset);
        Assert.Equal(258, result.DictionaryEntries[15].Offset);
        Assert.Equal(260, result.DictionaryEntries[16].Offset);
        Assert.Equal(262, result.DictionaryEntries[17].Offset);
        Assert.Equal(264, result.DictionaryEntries[18].Offset);
        Assert.Equal(266, result.DictionaryEntries[19].Offset);
        Assert.Equal(268, result.DictionaryEntries[20].Offset);
        Assert.Equal(270, result.DictionaryEntries[21].Offset);
        Assert.Equal(272, result.DictionaryEntries[22].Offset);
        Assert.Equal(274, result.DictionaryEntries[23].Offset);
        Assert.Equal(276, result.DictionaryEntries[24].Offset);
        Assert.Equal(278, result.DictionaryEntries[25].Offset);
        Assert.Equal(280, result.DictionaryEntries[26].Offset);
        Assert.Equal(282, result.DictionaryEntries[27].Offset);
        Assert.Equal(284, result.DictionaryEntries[28].Offset);
        Assert.Equal(286, result.DictionaryEntries[29].Offset);
        Assert.Equal(288, result.DictionaryEntries[30].Offset);
        Assert.Equal(290, result.DictionaryEntries[31].Offset);
        Assert.Equal(292, result.DictionaryEntries[32].Offset);
        Assert.Equal(294, result.DictionaryEntries[33].Offset);
        Assert.Equal(296, result.DictionaryEntries[34].Offset);
        Assert.Equal(298, result.DictionaryEntries[35].Offset);
        Assert.Equal(300, result.DictionaryEntries[36].Offset);
        Assert.Equal(302, result.DictionaryEntries[37].Offset);
        Assert.Equal(304, result.DictionaryEntries[38].Offset);
        Assert.Equal(306, result.DictionaryEntries[39].Offset);
        Assert.Equal(308, result.DictionaryEntries[40].Offset);
        Assert.Equal(310, result.DictionaryEntries[41].Offset);
        Assert.Equal(312, result.DictionaryEntries[42].Offset);
        Assert.Equal(314, result.DictionaryEntries[43].Offset);
        Assert.Equal(316, result.DictionaryEntries[44].Offset);
        Assert.Equal(318, result.DictionaryEntries[45].Offset);
        Assert.Equal(320, result.DictionaryEntries[46].Offset);
        Assert.Equal(322, result.DictionaryEntries[47].Offset);
        Assert.Equal(324, result.DictionaryEntries[48].Offset);
        Assert.Equal(326, result.DictionaryEntries[49].Offset);
        Assert.Equal(328, result.DictionaryEntries[50].Offset);
        Assert.Equal(330, result.DictionaryEntries[51].Offset);
        Assert.Equal(332, result.DictionaryEntries[52].Offset);
        Assert.Equal(334, result.DictionaryEntries[53].Offset);
        Assert.Equal(336, result.DictionaryEntries[54].Offset);
        Assert.Equal(338, result.DictionaryEntries[55].Offset);
        Assert.Equal(340, result.DictionaryEntries[56].Offset);
        Assert.Equal(342, result.DictionaryEntries[57].Offset);
        Assert.Equal(344, result.DictionaryEntries[58].Offset);
        Assert.Equal(346, result.DictionaryEntries[59].Offset);
        Assert.Equal(348, result.DictionaryEntries[60].Offset);
        Assert.Equal(350, result.DictionaryEntries[61].Offset);
        Assert.Equal(352, result.DictionaryEntries[62].Offset);
        Assert.Equal(354, result.DictionaryEntries[63].Offset);
        Assert.Equal(356, result.DictionaryEntries[64].Offset);
        Assert.Equal(358, result.DictionaryEntries[65].Offset);
        Assert.Equal(360, result.DictionaryEntries[66].Offset);
        Assert.Equal(362, result.DictionaryEntries[67].Offset);
        Assert.Equal(364, result.DictionaryEntries[68].Offset);
        Assert.Equal(366, result.DictionaryEntries[69].Offset);
        Assert.Equal(368, result.DictionaryEntries[70].Offset);
        Assert.Equal(370, result.DictionaryEntries[71].Offset);
        Assert.Equal(372, result.DictionaryEntries[72].Offset);
        Assert.Equal(375, result.DictionaryEntries[73].Offset);
        Assert.Equal(378, result.DictionaryEntries[74].Offset);
        Assert.Equal(381, result.DictionaryEntries[75].Offset);
        Assert.Equal(384, result.DictionaryEntries[76].Offset);
        Assert.Equal(387, result.DictionaryEntries[77].Offset);
        Assert.Equal(390, result.DictionaryEntries[78].Offset);
        Assert.Equal(393, result.DictionaryEntries[79].Offset);
        Assert.Equal(396, result.DictionaryEntries[80].Offset);
        Assert.Equal(399, result.DictionaryEntries[81].Offset);
        Assert.Equal(402, result.DictionaryEntries[82].Offset);
        Assert.Equal(405, result.DictionaryEntries[83].Offset);
        Assert.Equal(408, result.DictionaryEntries[84].Offset);
        Assert.Equal(411, result.DictionaryEntries[85].Offset);
        Assert.Equal(414, result.DictionaryEntries[86].Offset);
        Assert.Equal(417, result.DictionaryEntries[87].Offset);
        Assert.Equal(420, result.DictionaryEntries[88].Offset);
        Assert.Equal(423, result.DictionaryEntries[89].Offset);
        Assert.Equal(426, result.DictionaryEntries[90].Offset);
        Assert.Equal(429, result.DictionaryEntries[91].Offset);
        Assert.Equal(432, result.DictionaryEntries[92].Offset);
        Assert.Equal(435, result.DictionaryEntries[93].Offset);
        Assert.Equal(438, result.DictionaryEntries[94].Offset);
        Assert.Equal(441, result.DictionaryEntries[95].Offset);
        Assert.Equal(444, result.DictionaryEntries[96].Offset);
        Assert.Equal(447, result.DictionaryEntries[97].Offset);
        Assert.Equal(450, result.DictionaryEntries[98].Offset);
        Assert.Equal(453, result.DictionaryEntries[99].Offset);
        Assert.Equal(456, result.DictionaryEntries[100].Offset);
        Assert.Equal(459, result.DictionaryEntries[101].Offset);
        Assert.Equal(462, result.DictionaryEntries[102].Offset);
        Assert.Equal(465, result.DictionaryEntries[103].Offset);
        Assert.Equal(468, result.DictionaryEntries[104].Offset);
        Assert.Equal(472, result.DictionaryEntries[105].Offset);
        Assert.Equal(476, result.DictionaryEntries[106].Offset);
        Assert.Equal(480, result.DictionaryEntries[107].Offset);
        Assert.Equal(484, result.DictionaryEntries[108].Offset);
        Assert.Equal(488, result.DictionaryEntries[109].Offset);
        Assert.Equal(492, result.DictionaryEntries[110].Offset);
        Assert.Equal(496, result.DictionaryEntries[111].Offset);
        Assert.Equal(500, result.DictionaryEntries[112].Offset);
        Assert.Equal(2, result.DictionaryEntries[0].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[1].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[2].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[3].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[4].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[5].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[6].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[7].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[8].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[9].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[10].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[11].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[12].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[13].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[14].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[15].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[16].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[17].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[18].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[19].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[20].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[21].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[22].Data.Length);
        Assert.Equal(2, result.DictionaryEntries[23].Data.Length);
    }

    // Dictionary @0x000000B469DF6083

    // Entry count = 113                   Dictionary size (in bytes) = 504    Data section offset = 228
    // Data section start = 0x000000B469DF6167                                 Offset section start = 0x000000B469DF6085
    // 
    // Dictionary entries
    // 
    // Dictionary entry = Symbol  0, offset 228 length  2  | Dictionary entry = Symbol  1, offset 230 length  2 
    // Dictionary entry = Symbol  2, offset 232 length  2  | Dictionary entry = Symbol  3, offset 234 length  2 
    // Dictionary entry = Symbol  4, offset 236 length  2  | Dictionary entry = Symbol  5, offset 238 length  2 
    // Dictionary entry = Symbol  6, offset 240 length  2  | Dictionary entry = Symbol  7, offset 242 length  2 
    // Dictionary entry = Symbol  8, offset 244 length  2  | Dictionary entry = Symbol  9, offset 246 length  2 
    // Dictionary entry = Symbol 10, offset 248 length  2  | Dictionary entry = Symbol 11, offset 250 length  2 
    // Dictionary entry = Symbol 12, offset 252 length  2  | Dictionary entry = Symbol 13, offset 254 length  2 
    // Dictionary entry = Symbol 14, offset 256 length  2  | Dictionary entry = Symbol 15, offset 258 length  2 
    // Dictionary entry = Symbol 16, offset 260 length  2  | Dictionary entry = Symbol 17, offset 262 length  2 
    // Dictionary entry = Symbol 18, offset 264 length  2  | Dictionary entry = Symbol 19, offset 266 length  2 
    // Dictionary entry = Symbol 20, offset 268 length  2  | Dictionary entry = Symbol 21, offset 270 length  2 
    // Dictionary entry = Symbol 22, offset 272 length  2  | Dictionary entry = Symbol 23, offset 274 length  2 
    // Dictionary entry = Symbol 24, offset 276 length  2  | Dictionary entry = Symbol 25, offset 278 length  2 
    // Dictionary entry = Symbol 26, offset 280 length  2  | Dictionary entry = Symbol 27, offset 282 length  2 
    // Dictionary entry = Symbol 28, offset 284 length  2  | Dictionary entry = Symbol 29, offset 286 length  2 
    // Dictionary entry = Symbol 30, offset 288 length  2  | Dictionary entry = Symbol 31, offset 290 length  2 
    // Dictionary entry = Symbol 32, offset 292 length  2  | Dictionary entry = Symbol 33, offset 294 length  2 
    // Dictionary entry = Symbol 34, offset 296 length  2  | Dictionary entry = Symbol 35, offset 298 length  2 
    // Dictionary entry = Symbol 36, offset 300 length  2  | Dictionary entry = Symbol 37, offset 302 length  2 
    // Dictionary entry = Symbol 38, offset 304 length  2  | Dictionary entry = Symbol 39, offset 306 length  2 
    // Dictionary entry = Symbol 40, offset 308 length  2  | Dictionary entry = Symbol 41, offset 310 length  2 
    // Dictionary entry = Symbol 42, offset 312 length  2  | Dictionary entry = Symbol 43, offset 314 length  2 
    // Dictionary entry = Symbol 44, offset 316 length  2  | Dictionary entry = Symbol 45, offset 318 length  2 
    // Dictionary entry = Symbol 46, offset 320 length  2  | Dictionary entry = Symbol 47, offset 322 length  2 
    // Dictionary entry = Symbol 48, offset 324 length  2  | Dictionary entry = Symbol 49, offset 326 length  2 
    // Dictionary entry = Symbol 50, offset 328 length  2  | Dictionary entry = Symbol 51, offset 330 length  2 
    // Dictionary entry = Symbol 52, offset 332 length  2  | Dictionary entry = Symbol 53, offset 334 length  2 
    // Dictionary entry = Symbol 54, offset 336 length  2  | Dictionary entry = Symbol 55, offset 338 length  2 
    // Dictionary entry = Symbol 56, offset 340 length  2  | Dictionary entry = Symbol 57, offset 342 length  2 
    // Dictionary entry = Symbol 58, offset 344 length  2  | Dictionary entry = Symbol 59, offset 346 length  2 
    // Dictionary entry = Symbol 60, offset 348 length  2  | Dictionary entry = Symbol 61, offset 350 length  2 
    // Dictionary entry = Symbol 62, offset 352 length  2  | Dictionary entry = Symbol 63, offset 354 length  2 
    // Dictionary entry = Symbol 64, offset 356 length  2  | Dictionary entry = Symbol 65, offset 358 length  2 
    // Dictionary entry = Symbol 66, offset 360 length  2  | Dictionary entry = Symbol 67, offset 362 length  2 
    // Dictionary entry = Symbol 68, offset 364 length  2  | Dictionary entry = Symbol 69, offset 366 length  2 
    // Dictionary entry = Symbol 70, offset 368 length  2  | Dictionary entry = Symbol 71, offset 370 length  2 
    // Dictionary entry = Symbol 72, offset 372 length  3  | Dictionary entry = Symbol 73, offset 375 length  3 
    // Dictionary entry = Symbol 74, offset 378 length  3  | Dictionary entry = Symbol 75, offset 381 length  3 
    // Dictionary entry = Symbol 76, offset 384 length  3  | Dictionary entry = Symbol 77, offset 387 length  3 
    // Dictionary entry = Symbol 78, offset 390 length  3  | Dictionary entry = Symbol 79, offset 393 length  3 
    // Dictionary entry = Symbol 80, offset 396 length  3  | Dictionary entry = Symbol 81, offset 399 length  3 
    // Dictionary entry = Symbol 82, offset 402 length  3  | Dictionary entry = Symbol 83, offset 405 length  3 
    // Dictionary entry = Symbol 84, offset 408 length  3  | Dictionary entry = Symbol 85, offset 411 length  3 
    // Dictionary entry = Symbol 86, offset 414 length  3  | Dictionary entry = Symbol 87, offset 417 length  3 
    // Dictionary entry = Symbol 88, offset 420 length  3  | Dictionary entry = Symbol 89, offset 423 length  3 
    // Dictionary entry = Symbol 90, offset 426 length  3  | Dictionary entry = Symbol 91, offset 429 length  3 
    // Dictionary entry = Symbol 92, offset 432 length  3  | Dictionary entry = Symbol 93, offset 435 length  3 
    // Dictionary entry = Symbol 94, offset 438 length  3  | Dictionary entry = Symbol 95, offset 441 length  3 
    // Dictionary entry = Symbol 96, offset 444 length  3  | Dictionary entry = Symbol 97, offset 447 length  3 
    // Dictionary entry = Symbol 98, offset 450 length  3  | Dictionary entry = Symbol 99, offset 453 length  3 
    // Dictionary entry = Symbol 100, offset 456 length  3 | Dictionary entry = Symbol 101, offset 459 length  3
    // Dictionary entry = Symbol 102, offset 462 length  3 | Dictionary entry = Symbol 103, offset 465 length  3
    // Dictionary entry = Symbol 104, offset 468 length  4 | Dictionary entry = Symbol 105, offset 472 length  4
    // Dictionary entry = Symbol 106, offset 476 length  4 | Dictionary entry = Symbol 107, offset 480 length  4
    // Dictionary entry = Symbol 108, offset 484 length  4 | Dictionary entry = Symbol 109, offset 488 length  4
    // Dictionary entry = Symbol 110, offset 492 length  4 | Dictionary entry = Symbol 111, offset 496 length  4
    // Dictionary entry = Symbol 112, offset 500 length  4 | 
    private readonly byte[] dictionary = @" 7100e600 e800ea00 ec00ee00 f000f200 f400f600
                                            f800fa00 fc00fe00 00010201 04010601 08010a01
                                            0c010e01 10011201 14011601 18011a01 1c011e01
                                            20012201 24012601 28012a01 2c012e01 30013201
                                            34013601 38013a01 3c013e01 40014201 44014601
                                            48014a01 4c014e01 50015201 54015601 58015a01
                                            5c015e01 60016201 64016601 68016a01 6c016e01
                                            70017201 74017701 7a017d01 80018301 86018901
                                            8c018f01 92019501 98019b01 9e01a101 a401a701
                                            aa01ad01 b001b301 b601b901 bc01bf01 c201c501
                                            c801cb01 ce01d101 d401d801 dc01e001 e401e801
                                            ec01f001 f401f801 027e0280 02810283 02840285
                                            02860288 028a028c 028d028e 028f0290 02910292
                                            02930294 02950296 82c382c4 82c782c8 82ca82cb
                                            82e282e7 82ec830b 83298343 835a835b 83608361
                                            83638364 83658366 836c836d 83708371 83738374
                                            8375837f 8389838c 838e8395 8396839d 839e83a7
                                            83a883a9 83b483b9 83bd83bf 83c183c2 83c383c6
                                            83cc83d3 83dd83df 83e1f4f4 80ba5480 e443826d
                                            d1830ff0 83341484 926a84f5 478508d4 855ad785
                                            f1a0864c 4e86f117 86fde087 02ca8925 1e8a54a2
                                            8afc808c 06848d94 8e8db7d2 9c347e9d d0b2a5a5
                                            eca613c4 a91c7abb 5338be12 58c32a4e d18e98d4
                                            89e8f15b 46fb19c8 808577a8 809863e0 809a1294
                                            80a1a34c 80bae058 80fc2298 80fee1b8 8105ebf8
                                            810c6f38"
        .CleanHex()
        .ToByteArray();
}