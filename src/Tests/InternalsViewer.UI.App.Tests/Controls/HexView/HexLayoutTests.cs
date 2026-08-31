using InternalsViewer.UI.App.Controls.HexView;

namespace InternalsViewer.UI.App.Tests.Controls.HexView;

[Trait("Category", "Unit")]
public class HexLayoutTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(16, 1)]
    [InlineData(17, 2)]
    [InlineData(8192, 512)]
    public void GetLineCount_Rounds_A_Short_Last_Line_Up(int length, int expected)
    {
        Assert.Equal(expected, HexLayout.GetLineCount(length));
    }

    /// <remarks>
    /// The pitch is what every drawing over the hex is placed by, so counting a line either way tilts each position
    /// below the first by a fraction of the error.
    /// </remarks>
    [Fact]
    public void GetLineHeight_Divides_By_The_Lines_The_Text_Holds()
    {
        Assert.Equal(16, HexLayout.GetLineHeight(512 * 16, 8192, 16));
    }

    [Fact]
    public void GetLineHeight_Falls_Back_When_Nothing_Is_Rendered()
    {
        Assert.Equal(16, HexLayout.GetLineHeight(0, 8192, 16));
    }

    [Fact]
    public void GetLineHeight_Counts_A_Partial_Last_Line()
    {
        Assert.Equal(16, HexLayout.GetLineHeight(3 * 16, 33, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(255)]
    public void A_Byte_Position_Survives_The_Round_Trip(int position)
    {
        var runPosition = HexLayout.ToRunPosition(position);

        Assert.Equal(position, HexLayout.FromRunPosition(runPosition, HexLayout.CharactersPerLine));
    }

    [Fact]
    public void ToRunPosition_Counts_Three_Characters_A_Byte()
    {
        Assert.Equal(0, HexLayout.ToRunPosition(0));

        Assert.Equal(3, HexLayout.ToRunPosition(1));
    }

    /// <remarks>
    /// A line carries its own break, so the second line starts past the characters the first one took.
    /// </remarks>
    [Fact]
    public void ToRunPosition_Carries_The_Line_Break()
    {
        var second = HexLayout.ToRunPosition(HexLayout.BytesPerLine);

        Assert.Equal((int)HexLayout.CharactersPerLine, second);
    }

    [Fact]
    public void FromRunPosition_Reads_Both_Characters_Of_A_Byte_As_That_Byte()
    {
        var runPosition = HexLayout.ToRunPosition(5);

        Assert.Equal(5, HexLayout.FromRunPosition(runPosition, HexLayout.CharactersPerLine));

        Assert.Equal(5, HexLayout.FromRunPosition(runPosition + 1, HexLayout.CharactersPerLine));
    }
}
