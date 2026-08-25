using InternalsViewer.UI.App.Controls.HexView;

namespace InternalsViewer.UI.App.Tests.Controls.HexView;

public class HexTextBuilderTests
{
    private static byte[] Bytes(int count) => [.. Enumerable.Range(0, count).Select(i => (byte)i)];

    [Fact]
    public void Build_Writes_Every_Byte_As_Two_Characters()
    {
        var text = HexTextBuilder.Build(Bytes(16), 16);

        Assert.Equal("00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F", text);
    }

    [Fact]
    public void Build_Separates_Bytes_By_Space_Except_At_Line_End()
    {
        var lines = HexTextBuilder.Build(Bytes(32), 16).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Equal(16, line.Split(' ').Length));
        Assert.All(lines, line => Assert.False(line.EndsWith(' ')));
    }

    /// <remarks>
    /// A page divides into whole lines, however a columnstore blob is any length, so the bytes of a short last line
    /// have to be shown rather than dropped.
    /// </remarks>
    [Fact]
    public void Build_Keeps_A_Trailing_Partial_Line()
    {
        var lines = HexTextBuilder.Build(Bytes(20), 16).Split(Environment.NewLine);

        Assert.Equal(2, lines.Length);
        Assert.Equal("10 11 12 13", lines[1]);
    }

    /// <remarks>
    /// The newline separates lines rather than ending them, so the text does not run on past its last byte.
    /// </remarks>
    [Fact]
    public void Build_Does_Not_End_The_Text_With_A_Newline()
    {
        Assert.False(HexTextBuilder.Build(Bytes(32), 16).EndsWith(Environment.NewLine));
    }

    [Fact]
    public void Build_Given_No_Data_Gives_No_Text()
    {
        Assert.Equal(string.Empty, HexTextBuilder.Build([], 16));
    }
}
