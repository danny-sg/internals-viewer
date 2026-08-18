using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Engine.Columnstore;

public sealed class ColumnStoreColumn
{
    /// <summary>
    /// hbcolid from syscscolsegments
    /// </summary>
    public int ColumnStoreColumnId { get; set; }

    /// <summary>Null when no matching column layout was found - internal columns.</summary>
    public ColumnStructure? Structure { get; set; }

    public string Name => Structure?.ColumnName ?? $"(Column {ColumnStoreColumnId})";

    public bool IsInternal => Structure is null
                              || Structure.IsUniqueifier
                              || Structure.IsDropped;

    public SegmentDictionary? GlobalDictionary { get; set; }
}