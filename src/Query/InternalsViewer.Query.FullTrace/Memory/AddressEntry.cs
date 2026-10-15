using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct AddressEntry(ulong Address,
                                             ulong Call,
                                             ulong Owner,
                                             ulong Until,
                                             int Node,
                                             uint Thread,
                                             MemoryMapOperation Operation);
