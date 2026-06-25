using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Address;

public class LogSequenceNumberTests
{
    [Fact]
    public void ToString_Formats_Correctly()
    {
        var lsn = new LogSequenceNumber(1, 200, 3);

        Assert.Equal("(1:200:3)", lsn.ToString());
    }

    [Fact]
    public void ToBinaryString_Uses_Uppercase_Hex()
    {
        var lsn = new LogSequenceNumber(0x1A2B3C4D, 0x5E6F7080, 0x0102);

        Assert.Equal("1A2B3C4D:5E6F7080:0102", lsn.ToBinaryString());
    }

    [Fact]
    public void ToBinaryString_Pads_Correctly()
    {
        var lsn = new LogSequenceNumber(1, 1, 1);

        Assert.Equal("00000001:00000001:0001", lsn.ToBinaryString());
    }

    [Theory]
    [InlineData(0, 0, 0, "000000000000000")]   // "0"+"0000000000"+"00000"
    [InlineData(1, 0, 0, "1000000000000000")]  // "1"+"0000000000"+"00000"
    [InlineData(1, 500, 12, "1000000050000012")] // "1"+"0000000500"+"00012"
    public void ToDecimal_Produces_Expected_Value(int vlf, int fileOffset, short record, string expectedDecimalString)
    {
        var lsn = new LogSequenceNumber(vlf, fileOffset, record);

        Assert.Equal(decimal.Parse(expectedDecimalString), lsn.ToDecimal());
    }

    [Fact]
    public void ToDecimalFileOffsetOnly_Excludes_Record_Sequence()
    {
        var lsn = new LogSequenceNumber(5, 1000, 99);

        // Record sequence should not appear
        var withRecord = lsn.ToDecimal();
        var withoutRecord = lsn.ToDecimalFileOffsetOnly();

        Assert.True(withRecord > withoutRecord);
    }

    [Fact]
    public void Size_Is_Ten_Bytes()
    {
        Assert.Equal(10, LogSequenceNumber.Size);
    }
}
