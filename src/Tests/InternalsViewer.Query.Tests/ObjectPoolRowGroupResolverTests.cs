using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Tests;

public class ObjectPoolRowGroupResolverTests
{
    [Fact]
    public void Resolve_Looks_Up_The_Rowgroup_That_Uses_A_Secondary_Dictionary()
    {
        var single = Secondary(column: 8, dictionary: 4);

        var shared = Secondary(column: 3, dictionary: 1);

        var unknown = Secondary(column: 8, dictionary: 9);

        var otherIndex = new ObjectPoolEvent { HobtId = 7, ObjectType = ColumnStoreObjectType.SecondaryDictionary, ColumnId = 8, PoolObjectId = 4 };

        var segment = new ObjectPoolEvent { HobtId = 99, ObjectType = ColumnStoreObjectType.ColumnSegment, ColumnId = 8, PoolObjectId = 2, RowGroupId = 2 };

        ObjectPoolRowGroupResolver.Resolve([single, shared, unknown, otherIndex, segment], 99, [(8, 4, 3), (3, 1, 0), (3, 1, 1), (8, 2, 1)]);

        Assert.Equal(3, single.RowGroupId);
        Assert.Null(shared.RowGroupId);
        Assert.Null(unknown.RowGroupId);
        Assert.Null(otherIndex.RowGroupId);
        Assert.Equal(2, segment.RowGroupId);
    }

    private static ObjectPoolEvent Secondary(int column, int dictionary)
        => new() { HobtId = 99, ObjectType = ColumnStoreObjectType.SecondaryDictionary, ColumnId = column, PoolObjectId = dictionary };
}
