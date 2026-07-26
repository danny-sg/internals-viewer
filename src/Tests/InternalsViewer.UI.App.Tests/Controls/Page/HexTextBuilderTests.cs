using InternalsViewer.UI.App.Controls.Page;

namespace InternalsViewer.UI.App.Tests.Controls.Page;

public class HexTextBuilderTests
{
    private static byte[] Bytes(int count) => [.. Enumerable.Range(0, count).Select(i => (byte)i)];

    private static string Text(IEnumerable<HexRun> runs) => string.Concat(runs.Select(r => r.Text));

    [Fact]
    public void Build_Without_Selection_Gives_Single_Run()
    {
        var runs = HexTextBuilder.Build(Bytes(16), 16, null, null);

        Assert.Single(runs);
        Assert.False(runs[0].IsSelected);
        Assert.Equal("00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F" + Environment.NewLine, runs[0].Text);
    }

    [Fact]
    public void Build_Separates_Bytes_By_Space_Except_At_Line_End()
    {
        var runs = HexTextBuilder.Build(Bytes(32), 16, null, null);

        var lines = Text(runs).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Equal(16, line.Split(' ').Length));
        Assert.All(lines, line => Assert.False(line.EndsWith(' ')));
    }

    [Fact]
    public void Build_Marks_Selected_Range_As_A_Separate_Run()
    {
        var runs = HexTextBuilder.Build(Bytes(16), 16, 2, 4);

        var selected = runs.Where(r => r.IsSelected).ToList();

        Assert.Single(selected);
        Assert.Equal("02 03 04", selected[0].Text);
    }

    [Fact]
    public void Build_Marks_Single_Byte_Selection()
    {
        var runs = HexTextBuilder.Build(Bytes(16), 16, 5, 5);

        Assert.Equal("05", Assert.Single(runs.Where(r => r.IsSelected)).Text);
    }

    [Fact]
    public void Build_Selection_Does_Not_Alter_The_Overall_Text()
    {
        var plain = Text(HexTextBuilder.Build(Bytes(64), 16, null, null));
        var selected = Text(HexTextBuilder.Build(Bytes(64), 16, 3, 20));

        Assert.Equal(plain, selected);
    }

    [Fact]
    public void Build_Ignores_A_Trailing_Partial_Line()
    {
        var runs = HexTextBuilder.Build(Bytes(20), 16, null, null);

        Assert.Single(Text(runs).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Build_Given_No_Data_Gives_One_Empty_Run()
    {
        var runs = HexTextBuilder.Build([], 16, null, null);

        Assert.Equal(string.Empty, Assert.Single(runs).Text);
    }
}
