using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
[Trait("Area", "Helpers")]
public class TextLabelTests
{
    [Fact]
    public void A_Quoted_Label_Is_A_String_Without_Its_Quotes()
        => Assert.Equal(new StringValue("dbo.ClusteredTable", false), TextLabel.GetString("\"dbo.ClusteredTable\""));

    [Fact]
    public void A_Truncated_Quoted_Label_Keeps_Its_Truncation()
        => Assert.Equal(new StringValue("SELECT h.Id", true), TextLabel.GetString("\"SELECT h.Id\" (Truncated)"));

    [Theory]
    [InlineData("CQScanHash")]
    [InlineData("\"dbo\" + 0x8")]
    [InlineData("\"")]
    public void Any_Other_Label_Is_Not_A_String(string label) => Assert.Null(TextLabel.GetString(label));
}
