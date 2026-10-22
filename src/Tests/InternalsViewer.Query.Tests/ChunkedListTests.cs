using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ChunkedListTests
{
    [Fact]
    public void Items_Keep_Their_Order_Across_Chunks()
    {
        var list = new ChunkedList<int>();

        for (var index = 0; index < 150_000; index++)
        {
            list.Add(index);
        }

        var items = list.ToArray();

        Assert.Equal((150_000, 65_536, 149_999), (list.Count, list[65_536], items[^1]));
        Assert.True(items.Select((v, i) => v == i).All(m => m));
    }
}
