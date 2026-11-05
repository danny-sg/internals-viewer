
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

public sealed record AddressMapEntry(ulong Address,
                                     AddressMapOperation Operation,
                                     int CallIndex,
                                     ulong Owner,
                                     TracePosition FromPosition,
                                     TracePosition ToPosition,
                                     uint Thread,
                                     int CallNodeIndex,
                                     string? ClassName,
                                     string? Allocator,
                                     ulong Bytes = 0,
                                     string? Site = null,
                                     string? MemoryObject = null);
