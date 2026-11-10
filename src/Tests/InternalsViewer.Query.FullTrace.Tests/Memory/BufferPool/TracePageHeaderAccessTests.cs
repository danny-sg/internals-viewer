using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class TracePageHeaderAccessTests
{
    [Theory]
    [InlineData(1, 1, "Page Type")]
    [InlineData(3, 1, "Index Level")]
    [InlineData(22, 2, "Slot Count")]
    [InlineData(30, 2, "Free Data Offset")]
    [InlineData(32, 6, "Page Address")]
    [InlineData(40, 10, "Log Sequence Number")]
    [InlineData(50, 2, "Transaction Reserved")]
    public void A_Read_Of_A_Field_Is_Named_As_The_Page_Viewer_Names_It(int offset, int length, string expected)
        => Assert.Equal([expected], Fields(offset, length));

    [Fact]
    public void A_Read_Of_Part_Of_A_Field_Names_The_Field() => Assert.Equal(["Log Sequence Number"], Fields(42, 4));

    [Fact]
    public void A_Read_Spanning_Two_Fields_Names_Both() => Assert.Equal(["Free Data Offset", "Page Address"], Fields(30, 8));

    [Fact]
    public void Bytes_In_No_Field_Are_Named_By_Offset() => Assert.Equal(["Offset 0x40"], Fields(0x40, 4));

    [Fact]
    public void Separate_Unnamed_Reads_Are_Named_Separately()
    {
        var bytes = TracePageHeaderAccess.GetBytes(0x40, 2) | TracePageHeaderAccess.GetBytes(0x50, 2);

        Assert.Equal(["Offset 0x40", "Offset 0x50"], new TracePageHeaderAccess(default, bytes).GetFields());
    }

    [Fact]
    public void A_Read_Past_The_Header_Keeps_Only_Its_Header_Bytes() => Assert.Equal(["Offset 0x5F"], Fields(0x5F, 8));

    private static List<string> Fields(int offset, int length)
        => new TracePageHeaderAccess(default, TracePageHeaderAccess.GetBytes(offset, length)).GetFields();
}
