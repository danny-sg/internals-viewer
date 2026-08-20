namespace InternalsViewer.Internals.Columnstore.Metadata.Enums;

/// <summary>
/// Kind of row set backing a columnstore index, from sys.sysrowsets ownertype
/// </summary>
public enum ColumnstoreRowsetType : byte
{
    Unknown = 0,

    /// <summary>
    /// The columnstore itself, holding the compressed segments and dictionaries
    /// </summary>
    ColumnStore = 1,

    /// <summary>
    /// Rows logically deleted from compressed row groups, one row per deleted row
    /// </summary>
    DeleteBitmap = 2,

    /// <summary>
    /// Uncompressed rows for an open or closed row group
    /// </summary>
    DeltaStore = 3
}
