using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

[Trait("Category", "Unit")]
[Trait("Area", "BatchMode")]
public class DeletedRowsTests
{
    [Fact]
    public void None_Reports_Empty_For_Every_Row_Group()
    {
        Assert.True(DeletedRows.None.IsEmpty);

        Assert.Equal(0, DeletedRows.None.Count);

        Assert.Empty(DeletedRows.None.ForRowGroup(0));

        Assert.Empty(DeletedRows.None.ForRowGroup(7));
    }

    [Fact]
    public void Rows_Are_Returned_Per_Row_Group()
    {
        var deleted = new DeletedRows(new Dictionary<int, int[]>
        {
            [0] = [1, 4, 9],
            [3] = [2]
        });

        Assert.False(deleted.IsEmpty);

        Assert.Equal(4, deleted.Count);

        Assert.Equal([1, 4, 9], deleted.ForRowGroup(0));

        Assert.Equal([2], deleted.ForRowGroup(3));

        Assert.Empty(deleted.ForRowGroup(1));
    }
}
