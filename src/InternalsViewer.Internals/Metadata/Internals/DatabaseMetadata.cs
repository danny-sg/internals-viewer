using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Metadata.Internals;

public class InternalMetadata
{
    public List<InternalAllocationUnit> AllocationUnits { get; set; } = new();

    public List<InternalRowSet> RowSets { get; set; } = new();

    public List<InternalObject> Objects { get; set; } = new();

    public List<InternalColumn> Columns { get; set; } = new();

    public List<InternalEntityObject> Entities { get; set; } = new();

    public List<InternalIndex> Indexes { get; set; } = new();

    public List<InternalFile> Files { get; set; } = new();
}
