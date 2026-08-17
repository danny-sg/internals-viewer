using System.Collections.Specialized;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace;

public class BulkObservableCollectionTests
{
    [Fact]
    public void Refilling_Raises_One_Change_However_Many_Rows_Arrive()
    {
        var collection = new BulkObservableCollection<int>();

        var changes = new List<NotifyCollectionChangedAction>();

        collection.CollectionChanged += (_, e) => changes.Add(e.Action);

        collection.Reset([.. Enumerable.Range(1, 500)]);

        Assert.Equal([NotifyCollectionChangedAction.Reset], changes);
    }

    [Fact]
    public void Refilling_Replaces_The_Contents()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };

        collection.Reset([7, 8]);

        Assert.Equal([7, 8], collection);
    }

    [Fact]
    public void Refilling_With_Nothing_Empties_The_Collection()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };

        collection.Reset([]);

        Assert.Empty(collection);
    }

    [Fact]
    public void The_Collection_Instance_Is_Kept()
    {
        var collection = new BulkObservableCollection<int> { 1 };

        var before = collection;

        collection.Reset([2, 3]);

        Assert.Same(before, collection);
    }
}
