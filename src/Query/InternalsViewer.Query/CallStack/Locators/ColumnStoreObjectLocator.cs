using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct ColumnStoreObjectLocator(Locator Locator)
{
    private const ulong ObjectTypeMask = 0xFFFF;

    public ulong HobtId => Locator.Value & ~ObjectTypeMask;

    public ColumnStoreObjectType ObjectType => (ColumnStoreObjectType)(Locator.Value & ObjectTypeMask);

    public int ColumnId => (int)(uint)Locator.Detail;

    public int ObjectId => (int)(Locator.Detail >> 32);

    public int? RowGroupId => ObjectType is ColumnStoreObjectType.ColumnSegment or ColumnStoreObjectType.DeleteBitmap
        ? ObjectId
        : null;

    public string SearchText => RowGroupId switch
    {
        { } rowGroup when ColumnId >= 0 => $"rowgroup:{rowGroup} column:{ColumnId}",
        { } rowGroup => $"rowgroup:{rowGroup}",
        _ => $"column:{ColumnId}"
    };

    public static Locator? Read(IMemoryReader memory, int register, ulong address)
        => memory.TryRead(address, out ulong first)
           && memory.TryRead(address + sizeof(ulong), out ulong second)
           && memory.TryRead(address + 2 * sizeof(ulong), out ulong third)
            ? Create(register, first, second, third)
            : null;

    public static Locator Create(int register, ulong first, ulong second, ulong third)
    {
        var hobtId = (((ulong)(ushort)second << 32) | (uint)(first >> 32)) << 16;

        var objectType = (ushort)(third >> 32);

        var detail = ((ulong)(uint)third << 32) | (uint)(second >> 32);

        return new Locator(register, LocatorValueType.ColumnStoreObject, hobtId | objectType, detail);
    }

    public override string ToString() => ColumnStoreObjectLabel.Get(ObjectType, ColumnId, ObjectId);
}
