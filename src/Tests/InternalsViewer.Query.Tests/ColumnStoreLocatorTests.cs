using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.TimeTravel.CallLog;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ColumnStoreLocatorTests
{
    private const ulong HobtId = 0x0100_0000_02A7_0000;

    [Fact]
    public void A_Key_Is_Read_From_Its_Three_Words()
    {
        var locator = Key(ColumnStoreObjectType.ColumnSegment, column: 2, objectId: 3);

        Assert.Equal((HobtId, ColumnStoreObjectType.ColumnSegment, 2, 3, 3),
                     (locator.HobtId, locator.ObjectType, locator.ColumnId, locator.ObjectId, locator.RowGroupId));

        Assert.Equal("Segment (Rowgroup 3, Column 2)", locator.ToString());
        Assert.Equal("rowgroup:3 column:2", locator.SearchText);
    }

    [Theory]
    [InlineData(ColumnStoreObjectType.PrimaryDictionary, 2, 0, "Primary Dictionary (Column 2)", "column:2")]
    [InlineData(ColumnStoreObjectType.SecondaryDictionary, 2, 4, "Secondary Dictionary 4 (Column 2)", "column:2")]
    [InlineData(ColumnStoreObjectType.DeleteBitmap, -1, 1, "Delete Bitmap (Rowgroup 1)", "rowgroup:1")]
    public void Each_Object_Type_Is_Named(ColumnStoreObjectType type, int column, int objectId, string label, string search)
    {
        var locator = Key(type, column, objectId);

        Assert.Equal((label, search), (locator.ToString(), locator.SearchText));
    }

    [Fact]
    public void Only_Segments_And_Delete_Bitmaps_Belong_To_A_Rowgroup()
    {
        Assert.Null(Key(ColumnStoreObjectType.SecondaryDictionary, 2, 4).RowGroupId);
        Assert.Equal(1, Key(ColumnStoreObjectType.DeleteBitmap, -1, 1).RowGroupId);
    }

    [Theory]
    [InlineData("rowgroup:3", 3, null)]
    [InlineData("rg:3 col:2", 3, 2)]
    [InlineData("Column:2", null, 2)]
    public void A_Rowgroup_Or_Column_Search_Is_Read(string text, int? rowGroup, int? column)
    {
        Assert.True(LocatorSearch.TryParse(text, out var search));

        Assert.Equal(LocatorSearch.ForColumnStore(rowGroup, column), search);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rowgroup")]
    [InlineData("rowgroup:x")]
    [InlineData("segment:3")]
    [InlineData("1:336")]
    public void Anything_Else_Is_Not_A_Columnstore_Search(string text)
        => Assert.False(LocatorSearch.TryParse(text, out var search) && search.IsColumnStore);

    [Fact]
    public void A_Search_Matches_On_Every_Part_It_Names()
    {
        var segment = Key(ColumnStoreObjectType.ColumnSegment, 2, 3);

        Assert.True(LocatorSearch.ForColumnStore(3, null).Matches(segment));
        Assert.True(LocatorSearch.ForColumnStore(3, 2).Matches(segment));
        Assert.False(LocatorSearch.ForColumnStore(3, 1).Matches(segment));
        Assert.False(LocatorSearch.ForColumnStore(3, null).Matches(Key(ColumnStoreObjectType.SecondaryDictionary, 2, 3)));
    }

    [Fact]
    public void A_Node_Counts_Its_Columnstore_Objects()
    {
        var set = new TimeTravelLocatorSet();

        set.Add(Key(ColumnStoreObjectType.ColumnSegment, 2, 3));
        set.Add(Key(ColumnStoreObjectType.ColumnSegment, 2, 3));

        Assert.Equal("Segment (Rowgroup 3, Column 2)", set.Label);

        set.Add(Key(ColumnStoreObjectType.PrimaryDictionary, 2, 0));

        Assert.Equal("2 Columnstore Objects", set.Label);
        Assert.True(set.Contains(LocatorSearch.ForColumnStore(3, 2)));
        Assert.False(set.Contains(LocatorSearch.ForColumnStore(4, null)));
    }

    [Fact]
    public void A_Key_Is_Only_Kept_Once_All_Three_Words_Arrive()
    {
        var builder = new TimeTravelCallLog.Builder(LocatorReads.For(0x1000, Fix()));

        builder.Add(0x1000, 0, Columns(2), 2);

        var (first, second, third) = Words(ColumnStoreObjectType.ColumnSegment, 2, 3);

        builder.AddLocators([new TimeTravelMemoryValue(0x1000, 0, first, 0, 1, 0),
                             new TimeTravelMemoryValue(0x1000, 0, second, 0, 1, 8),
                             new TimeTravelMemoryValue(0x1000, 0, third, 0, 1, 16),
                             new TimeTravelMemoryValue(0x1000, 0, first, 1, 1, 0)]);

        var calls = builder.Build().CallsOf(0x1000, 0)!;

        Assert.Equal("Segment (Rowgroup 3, Column 2)", calls[0].LocatorAt(1)?.ToString());
        Assert.Empty(calls[1].Locators);
    }

    [Fact]
    public void The_Native_Structures_Match_The_Bridge()
    {
        Assert.Equal(40, Marshal.SizeOf<TimeTravelMemoryRead>());
        Assert.Equal(40, Marshal.SizeOf<TimeTravelMemoryValue>());
    }

    private static CallStack.Arguments.FunctionSignature Fix()
        => CallStack.Arguments.FunctionSignature.Parse(
            "ColumnStoreObjectRef::Fix(ColumnStoreObjectKey const &,bool,HoBtAccess *,XDES *,DeserializeFromDiskConstructor *)",
            "?Fix@ColumnStoreObjectRef@@QEAAPEAVColumnStoreObject@@AEBVColumnStoreObjectKey@@_NPEAVHoBtAccess@@PEAVXDES@@"
            + "PEAUDeserializeFromDiskConstructor@@@Z")!;

    private static TimeTravelLocator Key(ColumnStoreObjectType type, int column, int objectId)
    {
        var (first, second, third) = Words(type, column, objectId);

        return TimeTravelLocator.ColumnStoreObject(1, first, second, third);
    }

    private static (ulong First, ulong Second, ulong Third) Words(ColumnStoreObjectType type, int column, int objectId)
    {
        const ulong databaseId = 5;

        var shifted = HobtId >> 16;

        return (databaseId | ((ulong)(uint)shifted << 32),
                (ushort)(shifted >> 32) | ((ulong)(uint)column << 32),
                (uint)objectId | ((ulong)(uint)type << 32));
    }

    private static ulong[] Columns(int calls) => new ulong[calls * TimeTravelArgumentCall.ValueCount];
}
