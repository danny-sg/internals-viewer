using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct AddressEntry(ulong Address,
                                             ulong Frame,
                                             int CallIndex,
                                             ulong Owner,
                                             TracePosition FromPosition,
                                             TracePosition ToPosition,
                                             int CallNodeIndex,
                                             uint Thread,
                                             MemoryMapOperation Operation,
                                             string? ClassName = null,
                                             ulong Bytes = 0,
                                             string? Site = null)
{
    public bool IsAlive => !ToPosition.IsKnown;
}
