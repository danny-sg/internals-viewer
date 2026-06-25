using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Metadata.Internals;

public sealed class InternalMetadata
{
    /// <summary>
    /// Allocation Units table - sys.sysallocunits
    /// </summary>
    /// <remarks>Keyed by AllocationUnitId (auid).</remarks>
    public Dictionary<long, InternalAllocationUnit> AllocationUnits { get; set; } = [];

    /// <summary>
    /// Row Sets table - sys.sysrowsets
    /// </summary>
    /// <remarks>Keyed by RowSetId (rowsetid).</remarks>
    public Dictionary<long, InternalRowSet> RowSets { get; set; } = [];

    /// <summary>
    /// Object table - sys.sysschobjs
    /// </summary>
    /// <remarks>Keyed by ObjectId (id).</remarks>
    public Dictionary<int, InternalObject> Objects { get; set; } = [];

    /// <summary>
    /// Columns physical layout table - sys.sysrscols
    /// </summary>
    /// <remarks>Grouped by PartitionId (rsid).</remarks>
    public ILookup<long, InternalColumnLayout> ColumnLayouts { get; set; } = Enumerable.Empty<InternalColumnLayout>().ToLookup(c => c.PartitionId);

    /// <summary>
    /// Columns table - sys.syscolpars
    /// </summary>
    /// <remarks>Grouped by ObjectId (id).</remarks>
    public ILookup<int, InternalColumn> Columns { get; set; } = Enumerable.Empty<InternalColumn>().ToLookup(c => c.ObjectId);

    /// <summary>
    /// Entities table - sys.sysclsobjs
    /// </summary>
    /// <remarks>Keyed by (Id, ClassId) (id, class).</remarks>
    public Dictionary<(int Id, byte ClassId), InternalEntityObject> Entities { get; set; } = [];

    /// <summary>
    /// Indexes table - sys.sysidxstats
    /// </summary>
    /// <remarks>Grouped by ObjectId (id).</remarks>
    public ILookup<int, InternalIndex> Indexes { get; set; } = Enumerable.Empty<InternalIndex>().ToLookup(i => i.ObjectId);

    /// <summary>
    /// Index Columns table - sys.sysiscols
    /// </summary>
    /// <remarks>Grouped by (ObjectId, IndexId) (idmajor, idminor).</remarks>
    public ILookup<(int ObjectId, int IndexId), InternalIndexColumn> IndexColumns { get; set; } = Enumerable.Empty<InternalIndexColumn>().ToLookup(c => (c.ObjectId, c.IndexId));

    /// <summary>
    /// Files table - sys.sysprufiles
    /// </summary>
    public List<InternalFile> Files { get; set; } = [];
}
