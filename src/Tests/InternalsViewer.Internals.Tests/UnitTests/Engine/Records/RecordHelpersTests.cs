using InternalsViewer.Internals.Engine.Records;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Records;

public class RecordHelpersTests
{
    [Theory]
    [InlineData(0x8010, 0x0010)]  // High bit set, should be flipped off
    [InlineData(0x8000, 0x0000)]  // Just the high bit
    [InlineData(0xFFFF, 0x7FFF)]  // All bits set
    [InlineData(0x0010, 0x0010)]  // High bit not set, value unchanged
    [InlineData(0x0000, 0x0000)]  // Zero unchanged
    [InlineData(0x7FFF, 0x7FFF)]  // Max without high bit, unchanged
    public void DecodeOffset_Flips_High_Bit_Only(ushort input, ushort expected)
    {
        Assert.Equal(expected, RecordHelpers.DecodeOffset(input));
    }

    [Fact]
    public void GetOffsetArray_Reads_Correct_Number_Of_Entries()
    {
        // 3 ushort values at offset 2
        var data = new byte[] { 0x00, 0x00, 0x60, 0x00, 0x74, 0x00, 0x88, 0x00 };

        var result = RecordHelpers.GetOffsetArray(data, 3, 2);

        Assert.Equal(3, result.Length);
        Assert.Equal(0x0060, result[0]);
        Assert.Equal(0x0074, result[1]);
        Assert.Equal(0x0088, result[2]);
    }

    [Fact]
    public void GetOffsetArray_Reads_From_Start_When_Offset_Zero()
    {
        var data = new byte[] { 0x01, 0x00, 0x02, 0x00 };

        var result = RecordHelpers.GetOffsetArray(data, 2, 0);

        Assert.Equal((ushort)1, result[0]);
        Assert.Equal((ushort)2, result[1]);
    }

    [Fact]
    public void GetOffsetArray_Returns_Empty_When_Size_Zero()
    {
        var data = new byte[] { 0x01, 0x02 };

        var result = RecordHelpers.GetOffsetArray(data, 0, 0);

        Assert.Empty(result);
    }

    [Fact]
    public void GetArrayString_Formats_Entries_With_Decimal_And_Hex()
    {
        var array = new ushort[] { 96, 110 };

        var result = RecordHelpers.GetArrayString(array);

        Assert.Contains("96", result);
        Assert.Contains("110", result);
        Assert.Contains("0x60", result.ToLowerInvariant());
        Assert.Contains("0x6e", result.ToLowerInvariant());
    }

    [Fact]
    public void GetArrayString_Single_Entry_Has_No_Separator()
    {
        var array = new ushort[] { 128 };

        var result = RecordHelpers.GetArrayString(array);

        Assert.DoesNotContain(",", result);
    }

    [Fact]
    public void GetArrayString_Multiple_Entries_Are_Comma_Separated()
    {
        var array = new ushort[] { 1, 2, 3 };

        var result = RecordHelpers.GetArrayString(array);

        Assert.Equal(2, result.Count(c => c == ','));
    }
}
