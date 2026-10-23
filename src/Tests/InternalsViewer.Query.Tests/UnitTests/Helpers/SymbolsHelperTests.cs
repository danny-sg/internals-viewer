using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Tests.UnitTests.Helpers;

[Trait("Category", "Unit")]
public class SymbolsHelperTests
{
    [Theory]
    [InlineData("CQScanTableScanNew::GetRow+0x1a3", "CQScanTableScanNew::GetRow", 0x1A3u)]
    [InlineData("CQScanTop<1>::GetRow+0x10", "CQScanTop<1>::GetRow", 0x10u)]
    [InlineData("memcpy+40", "memcpy", 0x40u)]
    [InlineData("memcpy+ 0x40 ", "memcpy", 0x40u)]
    public void SplitOffset_Separates_The_Function_From_Its_Hex_Offset(string symbol, string name, uint offset)
    {
        SymbolsHelper.SplitOffset(symbol, out var actualName, out var actualOffset);

        Assert.Equal((name, offset), (actualName, actualOffset));
    }

    [Fact]
    public void SplitOffset_Leaves_A_Symbol_Without_An_Offset_Whole()
    {
        SymbolsHelper.SplitOffset("CQScanTableScanNew::GetRow", out var name, out var offset);

        Assert.Equal(("CQScanTableScanNew::GetRow", (uint?)null), (name, offset));
    }

    [Fact]
    public void SplitOffset_Has_No_Offset_When_It_Is_Not_Hex()
    {
        SymbolsHelper.SplitOffset("Function+zz", out var name, out var offset);

        Assert.Equal(("Function", (uint?)null), (name, offset));
    }
}
