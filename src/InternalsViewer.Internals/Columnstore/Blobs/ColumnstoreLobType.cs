namespace InternalsViewer.Internals.Columnstore.Blobs;

/// <summary>
/// Columnstore LOB type
/// </summary>
public enum ColumnstoreLobType
{
    Unknown = 0,

    /// <summary>
    /// A column segment, being the blob a segment data pointer resolves to
    /// </summary>
    Segment = 1,

    NumericDictionary = 2,

    /// <summary>
    /// A dictionary of strings, which CSINDEX reports as Lobtype 3
    /// </summary>
    StringDictionary = 3
}
