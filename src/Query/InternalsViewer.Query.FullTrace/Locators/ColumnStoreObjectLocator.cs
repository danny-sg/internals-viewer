using System.Buffers.Binary;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Interfaces;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct ColumnStoreObjectLocator(Locator Locator)
{
    private const ulong ObjectTypeMask = 0xFFFF;

    private const int PackedHoBtIdShift = 16;

    public ulong HoBtId => Locator.Value & ~ObjectTypeMask;

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

    public static Locator? Read(ITraceMemoryReader memory, int argumentSlot, ulong address)
    {
        var layout = memory.Layout.ColumnStoreObjectKey;

        return TryRead(memory, address, layout.HoBtIdField, out var packedHoBtId)
               && TryRead(memory, address, layout.ObjectTypeField, out var objectType)
               && TryRead(memory, address, layout.ColumnIdField, out var columnId)
               && TryRead(memory, address, layout.ObjectIdField, out var objectId)
            ? Create(argumentSlot, packedHoBtId << PackedHoBtIdShift, (ColumnStoreObjectType)objectType, (int)columnId, (int)objectId)
            : null;
    }

    public static Locator Create(int argumentSlot, ulong hobtId, ColumnStoreObjectType objectType, int columnId, int objectId)
        => new(argumentSlot,
               LocatorValueType.ColumnStoreObject,
               hobtId | (ushort)objectType,
               ((ulong)(uint)objectId << 32) | (uint)columnId);

    public override string ToString() => ColumnStoreObjectLabel.Get(ObjectType, ColumnId, ObjectId);

    private static bool TryRead(IMemoryReader memory, ulong address, MemoryField field, out ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];

        bytes.Clear();

        var isRead = memory.Read(address + (ulong)field.Offset, bytes[..field.Length]) == field.Length;

        value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);

        return isRead;
    }
}
