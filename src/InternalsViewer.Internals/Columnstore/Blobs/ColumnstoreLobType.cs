namespace InternalsViewer.Internals.Columnstore.Blobs;

/// <summary>
/// Kind of columnstore blob, held in the blob header
/// </summary>
public enum ColumnstoreLobType
{
    Unknown = 0,
    NumericDictionary = 2,
    Segment = 3
}
