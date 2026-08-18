namespace InternalsViewer.Internals.Columnstore.Blobs;

/// <summary>
/// Columnstore LOB sub-type
/// </summary>
public enum SubLobType
{
    None = 0,
    Array = 1,
    HashTable = 2,
    StringStore = 4,
    StringPage = 5,
    CompressedStringPage = 6
}
