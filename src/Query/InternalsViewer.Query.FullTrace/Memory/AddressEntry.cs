using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct AddressEntry(ulong Address,
                                             ulong Frame,
                                             int Call,
                                             TracePosition FromPosition,
                                             ulong Owner,
                                             TracePosition ToPosition,
                                             int Node,
                                             uint Thread,
                                             MemoryMapOperation Operation)
{
    public bool IsAlive => !ToPosition.IsKnown;
}
