using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct Locator(int Register, LocatorKind Kind, ulong Value, ulong Detail = 0)
{
    private const ulong PageMask = 0xFFFF_FFFF_FFFF;

    private const ulong ObjectTypeMask = 0xFFFF;

    public PageAddress Page => new((short)(Value >> 32), (int)(uint)Value);

    public ushort Slot => (ushort)(Value >> 48);

    public ulong PageKey => Value & PageMask;

    public RowIdentifier Row => new(Page, Slot);

    public ulong HobtId => Value & ~ObjectTypeMask;

    public ColumnStoreObjectType ObjectType => (ColumnStoreObjectType)(Value & ObjectTypeMask);

    public int ColumnId => (int)(uint)Detail;

    public int ObjectId => (int)(Detail >> 32);

    public int? RowGroupId => ObjectType is ColumnStoreObjectType.ColumnSegment or ColumnStoreObjectType.DeleteBitmap
        ? ObjectId
        : null;

    public string SearchText => Kind switch
    {
        LocatorKind.ColumnStoreObject when RowGroupId is { } rowGroup && ColumnId >= 0 
            => $"rowgroup:{rowGroup} column:{ColumnId}",
        LocatorKind.ColumnStoreObject when RowGroupId is { } rowGroup 
            => $"rowgroup:{rowGroup}",
        LocatorKind.ColumnStoreObject 
            => $"column:{ColumnId}",
        _ => ToString()
    };

    public static Locator ColumnStoreObject(int register, ulong first, ulong second, ulong third)
    {
        var hobtId = (((ulong)(ushort)second << 32) | (uint)(first >> 32)) << 16;

        var objectType = (ushort)(third >> 32);

        var detail = ((ulong)(uint)third << 32) | (uint)(second >> 32);

        return new Locator(register, LocatorKind.ColumnStoreObject, hobtId | objectType, detail);
    }

    public override string ToString() => Kind switch
    {
        LocatorKind.Row 
            => $"({Page.FileId}:{Page.PageId}:{Slot})",
        LocatorKind.ColumnStoreObject 
            => ObjectLabel(),
        _ => Page.ToString()
    };

    private string ObjectLabel() => ObjectType switch
    {
        ColumnStoreObjectType.ColumnSegment 
            => $"Segment (Rowgroup {ObjectId}, Column {ColumnId})",
        ColumnStoreObjectType.PrimaryDictionary 
            => $"Primary Dictionary (Column {ColumnId})",
        ColumnStoreObjectType.SecondaryDictionary 
            => $"Secondary Dictionary {ObjectId} (Column {ColumnId})",
        ColumnStoreObjectType.BulkInsertDictionary 
            => $"Bulk Insert Dictionary (Column {ColumnId})",
        ColumnStoreObjectType.DeleteBitmap 
            => $"Delete Bitmap (Rowgroup {ObjectId})",
        _ => $"{ObjectType} (Column {ColumnId}, Object {ObjectId})"
    };
}
