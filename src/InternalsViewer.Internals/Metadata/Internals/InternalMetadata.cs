using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Metadata.Internals;

public class InternalMetadata
{
    /// <summary>
    /// Allocation Units table - sys.sysallocunits
    /// </summary>
    public List<InternalAllocationUnit> AllocationUnits { get; set; } = new();

    /// <summary>
    /// Row Sets table - sys.sysrscols
    /// </summary>
    public List<InternalRowSet> RowSets { get; set; } = new();

    /// <summary>
    /// Object table - sys.sysschobjs
    /// </summary>
    public List<InternalObject> Objects { get; set; } = new();

    /// <summary>
    /// Columns physical layout table - sys.sysrscols
    /// </summary>
    public List<InternalColumnLayout> ColumnLayouts { get; set; } = new();

    /// <summary>
    /// Columns table - sys.sys.syscolpars
    /// </summary>
    public List<InternalColumn> Columns { get; set; } = new();

    /// <summary>
    /// Entities table - sys.sysclsobjs
    /// </summary>
    public List<InternalEntityObject> Entities { get; set; } = new();

    /// <summary>
    /// Indexes table - sys.sysidxstats
    /// </summary>
    public List<InternalIndex> Indexes { get; set; } = new();

    /// <summary>
    /// Index Columns table - sys.sysiscols
    /// </summary>
    public List<InternalIndexColumn> IndexColumns { get; set; } = new();

    /// <summary>
    /// Files table - sys.sysprufiles
    /// </summary>
    public List<InternalFile> Files { get; set; } = new();
}
