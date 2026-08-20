namespace InternalsViewer.Internals.Columnstore.Metadata.Enums;

public enum RowGroupState
{
    Invisible = 0,
    Open = 1,
    Closed = 2,
    Compressed = 3,
    Tombstone = 4
}