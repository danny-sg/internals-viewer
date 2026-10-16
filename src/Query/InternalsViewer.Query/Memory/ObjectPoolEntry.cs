using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Memory;

public sealed record ObjectPoolEntry(ulong EntryAddress,
                                     ulong DataAddress,
                                     ulong MemoryObjectAddress,
                                     long PagesKb,
                                     ColumnStoreObjectType ObjectType,
                                     int DatabaseId,
                                     ulong HobtId,
                                     int ColumnId,
                                     int ObjectId,
                                     long ObjectSize)
{
    public override string ToString() => ColumnStoreObjectLabel.Get(ObjectType, ColumnId, ObjectId);
}
