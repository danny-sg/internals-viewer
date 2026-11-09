using InternalsViewer.Query.FullTrace.Timeline.Tracks;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceWatchValueTests
{
    [Fact]
    public void A_Value_Is_Not_Known_Until_All_Its_Bytes_Have_Been_Written()
    {
        var value = new TraceWatchValue(0x40, 2);

        Assert.Equal((false, true), (Write(value, 0x40, [0x01]) is not null, Write(value, 0x41, [0x00]) is not null));
    }

    [Fact]
    public void A_Write_Only_Changes_The_Bytes_It_Overlaps()
    {
        var value = new TraceWatchValue(0x40, 2);

        Assert.Equal(new byte[] { 0x02, 0x03 }, Write(value, 0x3F, [0x01, 0x02, 0x03, 0x04]));
    }

    [Fact]
    public void Writing_The_Same_Value_Again_Is_Not_A_Change()
    {
        var value = new TraceWatchValue(0, 4);

        Write(value, 0, [0x05, 0x00, 0x00, 0x00]);

        var repeated = Write(value, 0, [0x05, 0x00, 0x00, 0x00]);

        var changed = Write(value, 0, [0x06]);

        Assert.Null(repeated);

        Assert.Equal(new byte[] { 0x06, 0x00, 0x00, 0x00 }, changed);
    }

    [Fact]
    public void A_Write_Outside_The_Value_Is_Ignored()
    {
        var value = new TraceWatchValue(0x10, 4);

        Assert.Null(value.GetOverlap(0x14, 8));
    }

    private static byte[]? Write(TraceWatchValue value, int offset, byte[] bytes)
    {
        if (value.GetOverlap(offset, bytes.Length) is not (var from, var to))
        {
            return null;
        }

        bytes.AsSpan(from - offset, to - from).CopyTo(value.GetTarget(from, to));

        return value.TakeChange();
    }
}
