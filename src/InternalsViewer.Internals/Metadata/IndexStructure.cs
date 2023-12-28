using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Metadata;

public class IndexStructure(long allocationUnitId) : Structure(allocationUnitId)
{
    public bool IsHeap { get; set; }

    public bool IsUnique { get; set; }

    public IndexType IndexType { get; set; }
    
    public bool HasFilter { get; set; }
}