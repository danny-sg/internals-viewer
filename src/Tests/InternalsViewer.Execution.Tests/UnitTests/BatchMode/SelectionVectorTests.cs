using InternalsViewer.Execution.BatchMode;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

public class SelectionVectorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(900)]
    public void Every_Row_Starts_Selected(int rowCount)
    {
        var selection = new SelectionVector(rowCount);

        Assert.Equal(rowCount, selection.RowCount);

        for (var i = 0; i < rowCount; i++)
        {
            Assert.Equal(i, selection[i]);

            Assert.True(selection.IsSelected(i));
        }
    }

    [Fact]
    public void Clearing_A_Row_Removes_It_From_The_Walk()
    {
        var selection = new SelectionVector(128);

        selection.Clear(0);

        selection.Clear(64);

        selection.Clear(127);

        Assert.Equal(125, selection.RowCount);

        Assert.False(selection.IsSelected(0));

        Assert.False(selection.IsSelected(64));

        Assert.False(selection.IsSelected(127));

        Assert.Equal(1, selection[0]);
    }

    [Fact]
    public void Clearing_Keeps_The_Remaining_Rows_In_Order()
    {
        var selection = new SelectionVector(200);

        for (var i = 0; i < 200; i += 3)
        {
            selection.Clear(i);
        }

        var walked = new List<int>();

        for (var i = 0; i < selection.RowCount; i++)
        {
            walked.Add(selection[i]);
        }

        Assert.Equal(walked.OrderBy(r => r), walked);

        Assert.Equal(walked, walked.Distinct());

        Assert.All(walked, r => Assert.NotEqual(0, r % 3));
    }

    [Fact]
    public void Clearing_A_Row_That_Is_Already_Gone_Changes_Nothing()
    {
        var selection = new SelectionVector(8);

        Assert.True(selection.Clear(3));

        Assert.False(selection.Clear(3));

        Assert.Equal(7, selection.RowCount);
    }

    [Fact]
    public void Removing_Everything_Leaves_Nothing_To_Walk()
    {
        var selection = new SelectionVector(100);

        selection.RemoveAll();

        Assert.Equal(0, selection.RowCount);

        Assert.False(selection.IsSelected(0));
    }

    [Fact]
    public void Rebuilding_In_Place_Keeps_Only_The_Rows_Added_Back()
    {
        var selection = new SelectionVector(10);

        var read = selection.RowCount;

        selection.RemoveAll();

        for (var i = 0; i < read; i++)
        {
            if (selection[i] % 2 == 0)
            {
                selection.Add(selection[i]);
            }
        }

        Assert.Equal(5, selection.RowCount);

        Assert.Equal([0, 2, 4, 6, 8], Enumerable.Range(0, selection.RowCount).Select(i => (int)selection[i]));
    }

    [Fact]
    public void Reset_Selects_Every_Row_Again()
    {
        var selection = new SelectionVector(16);

        selection.RemoveAll();

        selection.Reset();

        Assert.Equal(16, selection.RowCount);

        Assert.Equal(15, selection[15]);
    }
}
