namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public sealed record BatchField(string Name, int Offset, int Length)
{
    public bool IsPointer => Length == sizeof(ulong);
}
