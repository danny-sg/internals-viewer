namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistoryGraphElement(int Index, int Offset, int Size)
{
    public bool Contains(int offset) => offset >= Offset && offset < Offset + Size;
}
