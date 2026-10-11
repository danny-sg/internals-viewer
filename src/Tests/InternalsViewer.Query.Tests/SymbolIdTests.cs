using InternalsViewer.Query.CallStack.Symbols;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class SymbolIdTests
{
    [Theory]
    [InlineData("443e516b-95f8-4b05-ac2d-73655ee4f524", 2, "443E516B95F84B05AC2D73655EE4F5242")]
    [InlineData("443e516b-95f8-4b05-ac2d-73655ee4f524", 10, "443E516B95F84B05AC2D73655EE4F524A")]
    [InlineData("443e516b-95f8-4b05-ac2d-73655ee4f524", 1604, "443E516B95F84B05AC2D73655EE4F524644")]
    public void The_Age_Follows_The_Guid_In_Hex(string guid, int age, string expected)
        => Assert.Equal(expected, SymbolDownloader.SymbolId(guid, age));
}
