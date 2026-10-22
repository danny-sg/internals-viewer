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

    [Fact]
    public void A_Chunked_List_Reads_As_A_List()
    {
        var list = new ChunkedList<int>();

        for (var index = 0; index < 1_000; index++)
        {
            list.Add(index * 3);
        }

        IReadOnlyList<int> items = list;

        Assert.Equal(Enumerable.Range(0, 1_000).Select(i => i * 3), items);
        Assert.Equal(2_997, items[999]);
    }
}
