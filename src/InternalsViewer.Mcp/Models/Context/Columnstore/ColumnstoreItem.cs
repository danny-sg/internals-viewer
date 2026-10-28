namespace InternalsViewer.Mcp.Models.Context.Columnstore;

/// <summary>
/// An object of a columnstore index that opens in a tab of its own
/// </summary>
public enum ColumnstoreItem
{
    Segment,
    Dictionary,
    DeltaStore,
    DeleteBitmap
}