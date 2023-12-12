namespace InternalsViewer.Internals.Metadata;

public class IndexStructure(long allocationUnitId) : Structure(allocationUnitId)
{
    public bool IsHeap { get; set; }

    public bool IsUnique { get; set; }

    public byte IndexType { get; set; }
}