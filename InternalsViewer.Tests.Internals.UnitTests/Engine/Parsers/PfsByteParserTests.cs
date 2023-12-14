using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Extensions;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.UnitTests.Engine.Parsers;

public class PfsByteParserTests(ITestOutputHelper output)
{
    public ITestOutputHelper Output { get; set; } = output;

    [Theory]                                                                     // 87654321
    [InlineData(0, SpaceFree.Empty, false, false, false, false)]                 // 00000000
    [InlineData(0x01, SpaceFree.FiftyPercent, false, false, false, false)]       // 00000001
    [InlineData(0x02, SpaceFree.EightyPercent, false, false, false, false)]      // 00000010
    [InlineData(0x03, SpaceFree.NinetyFivePercent, false, false, false, false)]  // 00000011
    [InlineData(0x04, SpaceFree.OneHundredPercent, false, false, false, false)]  // 00000100

    [InlineData(0x40, SpaceFree.Empty, true, false, false, false)]               // 01000000
    [InlineData(0x41, SpaceFree.FiftyPercent, true, false, false, false)]        // 01000001
    [InlineData(0x42, SpaceFree.EightyPercent, true, false, false, false)]       // 01000010
    [InlineData(0x43, SpaceFree.NinetyFivePercent, true, false, false, false)]   // 01000011
    [InlineData(0x44, SpaceFree.OneHundredPercent, true, false, false, false)]   // 01000100

    [InlineData(0x60, SpaceFree.Empty, true, true, false, false)]                // 01100000
    [InlineData(0x61, SpaceFree.FiftyPercent, true, true, false, false)]         // 01100001
    [InlineData(0x62, SpaceFree.EightyPercent, true, true, false, false)]        // 01100010
    [InlineData(0x63, SpaceFree.NinetyFivePercent, true, true, false, false)]    // 01100011
    [InlineData(0x64, SpaceFree.OneHundredPercent, true, true, false, false)]    // 01100100
    public void Can_Parse_Pfs_To_Byte(byte input, 
                                      SpaceFree expectedSpaceFree, 
                                      bool expectedAllocated, 
                                      bool expectedMixed, 
                                      bool expectedIam, 
                                      bool expectedGhostRecords)
    {
        Output.WriteLine($"Input: {input:X2}, {input.ToBinaryString()}");

        var pfsByte = PfsByteParser.Parse(input);

        Assert.Equal(expectedSpaceFree, pfsByte.PageSpaceFree);
        Assert.Equal(expectedAllocated, pfsByte.Allocated);
        Assert.Equal(expectedMixed, pfsByte.Mixed);
        Assert.Equal(expectedIam, pfsByte.Iam);
        Assert.Equal(expectedGhostRecords, pfsByte.GhostRecords);
    }

    [Theory]
    [InlineData(0, SpaceFree.Empty, false, false, false, false)]                 // 00000000
    [InlineData(0x01, SpaceFree.FiftyPercent, false, false, false, false)]       // 00000001
    [InlineData(0x02, SpaceFree.EightyPercent, false, false, false, false)]      // 00000010
    [InlineData(0x03, SpaceFree.NinetyFivePercent, false, false, false, false)]  // 00000011
    [InlineData(0x04, SpaceFree.OneHundredPercent, false, false, false, false)]  // 00000100
    [InlineData(0x44, SpaceFree.OneHundredPercent, true, false, false, false)]   // 01000100
    [InlineData(0x64, SpaceFree.OneHundredPercent, true, true, false, false)]    // 01100100
    [InlineData(0x74, SpaceFree.OneHundredPercent, true, true, true, false)]     // 01110100
    [InlineData(0x7C, SpaceFree.OneHundredPercent, true, true, true, true)]      // 01111100

    [InlineData(0x41, SpaceFree.FiftyPercent, true, false, false, false)]        // 00000001
    [InlineData(0x61, SpaceFree.FiftyPercent, true, true, false, false)]         // 00000001
    [InlineData(0x71, SpaceFree.FiftyPercent, true, true, true, false)]          // 00000001
    [InlineData(0x79, SpaceFree.FiftyPercent, true, true, true, true)]           // 00000001
    public void Can_Parse_Byte_To_Pfs(byte expected,
                                      SpaceFree spaceFree,
                                      bool allocated,
                                      bool mixed,
                                      bool iam,
                                      bool ghostRecords)
    {
        var pfsByte = new PfsByte
        {
            PageSpaceFree = spaceFree,
            Allocated = allocated,
            Mixed = mixed,
            Iam = iam,
            GhostRecords = ghostRecords
        };

        Output.WriteLine($"Expected: {expected:X2}, {expected.ToBinaryString()}");

        var result = PfsByteParser.Parse(pfsByte);

        Output.WriteLine($"Actual: {result:X2}, {result.ToBinaryString()}");

        Assert.Equal(expected, result);
    }
}
