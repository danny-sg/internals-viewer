
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

internal readonly record struct AddressEntry(ulong Address,
                                             ulong Frame,
                                             int CallIndex,
                                             ulong Owner,
                                             TracePosition FromPosition,
                                             TracePosition ToPosition,
                                             int CallNodeIndex,
                                             uint Thread,
                                             AddressMapOperation Operation,
                                             string? ClassName = null,
                                             ulong Bytes = 0,
                                             string? Site = null,
                                             string? MemoryObject = null,
                                             string? VtableClassName = null)
{
    public bool IsAlive => !ToPosition.IsKnown;
}
