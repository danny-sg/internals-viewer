namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public sealed record MemoryHistoryGraphElement(int Index, int Offset, int Size)
{
    public bool Contains(int offset) => offset >= Offset && offset < Offset + Size;
}
