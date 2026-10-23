namespace InternalsViewer.Query.FullTrace.Tests;

public class TracePositionTests
{
    [Fact]
    public void Sequence_And_Steps_Round_Trip()
    {
        var position = new TracePosition(0x1E62DC, 0x270F);

        Assert.Equal(0x1E62DCul, position.Sequence);
        Assert.Equal(0x270Ful, position.Steps);
        Assert.True(position.IsKnown);
    }

    [Fact]
    public void Positions_Order_By_Sequence_Then_Steps()
    {
        Assert.True(new TracePosition(1, uint.MaxValue) < new TracePosition(2, 0));
        Assert.True(new TracePosition(2, 3) < new TracePosition(2, 4));
        Assert.Equal(new TracePosition(5, 6), new TracePosition(5, 5).Next);
    }

    [Fact]
    public void Unknown_Sorts_After_Every_Known_Position()
    {
        var unknown = new TracePosition(ulong.MaxValue, 7);

        Assert.Equal(TracePosition.Unknown, unknown);
        Assert.False(unknown.IsKnown);
        Assert.Equal(ulong.MaxValue, unknown.Sequence);
        Assert.True(new TracePosition(uint.MaxValue - 1, uint.MaxValue) < unknown);
    }

    [Theory]
    [InlineData(0xFFFF_FFFFul, 0ul)]
    [InlineData(0x1_0000_0000ul, 0ul)]
    [InlineData(1ul, 0x1_0000_0000ul)]
    public void A_Position_Outside_32_Bits_Is_Rejected(ulong sequence, ulong steps)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TracePosition(sequence, steps));
}
