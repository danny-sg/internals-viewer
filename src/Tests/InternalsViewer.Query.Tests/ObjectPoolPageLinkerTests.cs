using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Tests;

public class ObjectPoolPageLinkerTests
{
    private static readonly ColumnstorePageRead[] Reads =
    [
        new(new PageAddress(1, 10), 0, 3, "Amount", -1, ColumnstoreReadType.Segment),
        new(new PageAddress(1, 11), 0, 3, "Amount", -1, ColumnstoreReadType.Segment),
        new(new PageAddress(1, 20), 1, 3, "Amount", -1, ColumnstoreReadType.Segment),
        new(new PageAddress(1, 30), -1, 3, "Amount", 1, ColumnstoreReadType.Dictionary),
        new(new PageAddress(1, 40), 0, 3, "Amount", 2, ColumnstoreReadType.Dictionary)
    ];

    [Fact]
    public void Link_Gives_Each_Lookup_The_Pages_Of_Its_Own_Object()
    {
        var segment = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.ColumnSegment, RowGroupId = 0, ColumnId = 3 };

        var primary = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.PrimaryDictionary, RowGroupId = 1, ColumnId = 3 };

        var secondary = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.SecondaryDictionary, RowGroupId = 0, ColumnId = 3 };

        ObjectPoolPageLinker.Link([segment, primary, secondary], 99, Reads);

        Assert.Equal([new PageAddress(1, 10), new PageAddress(1, 11)], segment.Pages);
        Assert.Equal([new PageAddress(1, 30)], primary.Pages);
        Assert.Equal([new PageAddress(1, 40)], secondary.Pages);
    }

    [Fact]
    public void Link_Leaves_Lookups_It_Cannot_Place()
    {
        var otherIndex = new ObjectPoolEvent { HobtId = 7, ObjectType = ColumnStoreObjectType.ColumnSegment, RowGroupId = 0, ColumnId = 3 };

        var noRowGroup = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.ColumnSegment, ColumnId = 3 };

        var deleteBitmap = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.DeleteBitmap, RowGroupId = 0, ColumnId = -1 };

        ObjectPoolPageLinker.Link([otherIndex, noRowGroup, deleteBitmap], 99, Reads);

        Assert.Empty(otherIndex.Pages);
        Assert.Empty(noRowGroup.Pages);
        Assert.Empty(deleteBitmap.Pages);
    }
}
