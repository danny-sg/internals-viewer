using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Columnstore.Metadata;

public enum ColumnstoreReadType
{
    Segment,
    Dictionary,
    DeleteBitmap
}

public sealed record ColumnstorePageRead(PageAddress PageAddress,
                                         int RowGroupId,
                                         int ColumnId,
                                         string ColumnName,
                                         int SegmentId,
                                         int DictionaryId,
                                         ColumnstoreReadType ReadType,
                                         int Bytes = 0);
