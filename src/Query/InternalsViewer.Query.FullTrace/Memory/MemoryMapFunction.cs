using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryMapFunction(ulong Address, MemoryMapOperation Operation)
{
    public DeclaredClass[] Declared { get; init; } = [];
}
