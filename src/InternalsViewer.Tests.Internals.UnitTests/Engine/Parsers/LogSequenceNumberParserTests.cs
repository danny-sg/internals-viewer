using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Tests.Internals.UnitTests.Engine.Parsers;

public class LogSequenceNumberParserTests
{
    [Theory]
    [InlineData("0:0:0", 0, 0, 0)]
    [InlineData("(0:0:0)", 0, 0, 0)]
    [InlineData("1:2:3", 1, 2, 3)]
    [InlineData("(1:2:3)", 1, 2, 3)]
    [InlineData("(53:24384:559)", 53, 24384, 559)]
    [InlineData("00000035:000075B8:0002", 53, 30136, 2)]
    public void Can_Parse_Log_Sequence_Number(string value, int virtualLogFile, int fileOffset, int recordSequence)
    {
        var logSequenceNumber = LogSequenceNumberParser.Parse(value);

        Assert.Equal(virtualLogFile, logSequenceNumber.VirtualLogFile);
        Assert.Equal(fileOffset, logSequenceNumber.FileOffset);
        Assert.Equal(recordSequence, logSequenceNumber.RecordSequence);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 0, 0)]
    public void Can_Parse_Binary_Log_Sequence_Number(byte[] value, int virtualLogFile, int fileOffset, int recordSequence)
    {
        var logSequenceNumber = LogSequenceNumberParser.Parse(value);

        Assert.Equal(virtualLogFile, logSequenceNumber.VirtualLogFile);
        Assert.Equal(fileOffset, logSequenceNumber.FileOffset);
        Assert.Equal(recordSequence, logSequenceNumber.RecordSequence);
    }
} 