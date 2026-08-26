using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

public class SelectionBitmapTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(900)]
    public void Every_Position_Starts_Qualifying(int rowCount)
    {
        var selection = new SelectionBitmap(rowCount);

        Assert.Equal(rowCount, selection.Count);

        for (var i = 0; i < rowCount; i++)
        {
            Assert.True(selection.IsSet(i));
        }
    }

    [Fact]
    public void Positions_Past_The_Row_Count_Are_Never_Set()
    {
        var selection = new SelectionBitmap(65);

        Assert.Equal(65, selection.Count);

        Assert.Equal(-1, selection.GetNextSetIndex(65));
    }

    [Fact]
    public void Clearing_A_Position_Removes_It_From_The_Walk()
    {
        var selection = new SelectionBitmap(128);

        selection.Clear(0);

        selection.Clear(64);

        selection.Clear(127);

        Assert.Equal(125, selection.Count);

        Assert.Equal(1, selection.GetNextSetIndex(0));

        Assert.Equal(65, selection.GetNextSetIndex(64));

        Assert.Equal(-1, selection.GetNextSetIndex(127));
    }

    [Fact]
    public void An_Empty_Word_Is_Skipped_Whole()
    {
        var selection = new SelectionBitmap(192);

        for (var i = 0; i < 128; i++)
        {
            selection.Clear(i);
        }

        Assert.Equal(64, selection.Count);

        Assert.Equal(128, selection.GetNextSetIndex(0));
    }

    [Fact]
    public void Walking_Visits_Every_Qualifying_Position_Once()
    {
        var selection = new SelectionBitmap(200);

        for (var i = 0; i < 200; i += 3)
        {
            selection.Clear(i);
        }

        var visited = new List<int>();

        for (var position = selection.GetNextSetIndex(0); position >= 0; position = selection.GetNextSetIndex(position + 1))
        {
            visited.Add(position);
        }

        Assert.Equal(selection.Count, visited.Count);

        Assert.All(visited, p => Assert.True(selection.IsSet(p)));

        Assert.Equal(visited, visited.Distinct().OrderBy(p => p));
    }

    [Fact]
    public void Clearing_Everything_Leaves_Nothing_To_Walk()
    {
        var selection = new SelectionBitmap(100);

        selection.ClearAll();

        Assert.Equal(0, selection.Count);

        Assert.Equal(-1, selection.GetNextSetIndex(0));
    }
}
