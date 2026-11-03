namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public sealed record MemoryField(string Name, int Offset, int Length, bool IsHex = false)
{
    public bool IsPointer => Length == sizeof(ulong);
}
