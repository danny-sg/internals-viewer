namespace InternalsViewer.Query.FullTrace.Memory.Address;

internal readonly record struct AddressMapAllocation(ulong Bytes, string? Site, string? MemoryObject);
