using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record ReplayFunctionSet(ulong[] Excluded,
                                       MemoryFunction[] Memory,
                                       IReadOnlyDictionary<ulong, string> Publishers,
                                       ulong[] BufferReserves,
                                       ulong[] InstanceMethods)
{
    public ulong[] Markers { get; } = [.. Publishers.Keys, .. BufferReserves];
}
