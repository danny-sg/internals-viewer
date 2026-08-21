using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Columnstore.Metadata;

public sealed class ColumnStoreColumn
{
    /// <summary>
    /// hbcolid from syscscolsegments
    /// </summary>
    public int ColumnStoreColumnId { get; set; }

    /// <summary>Null when no matching column layout was found - internal columns.</summary>
    public ColumnStructure? Structure { get; set; }

    /// <summary>
    /// Whether the column is the locator a nonclustered index keeps to find its way back to the base row
    /// </summary>
    /// <remarks>
    /// A clustered columnstore is the data, so it has nothing to point at and carries no locator. A nonclustered one
    /// holds the RID over a heap and the clustered key over a clustered table, which is why its encoding differs.
    /// </remarks>
    public bool IsLocator { get; set; }

    /// <summary>
    /// What the locator holds, which the index it points into decides
    /// </summary>
    public string LocatorDescription { get; set; } = string.Empty;

    public string LocatorName { get; set; } = "Row Locator";

    public string Name => Structure?.ColumnName
                          ?? (IsLocator ? LocatorName : $"(Column {ColumnStoreColumnId})");

    public bool IsInternal => Structure is null
                              || Structure.IsUniqueifier
                              || Structure.IsDropped;

    public SegmentDictionary? GlobalDictionary { get; set; }
}