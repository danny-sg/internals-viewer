namespace InternalsViewer.Query.FullTrace.Memory.MemoryObject;

/// <summary>
/// Reference to an owning SQL Server Memory Object with its address, type, and page size
/// </summary>
public sealed record MemoryObjectReference(ulong Address, string? Type, int PageSize)
{
    public string? Name => Type != null ? MemoryObjectNameLookup.GetName(Type) : null;

    private ulong PageMask => ~((ulong)PageSize - 1);

    public string GetText(ulong value)
        => value >= Address && (value & PageMask) == (Address & PageMask)
            ? $"0x{Address:X} + 0x{value - Address:X}"
            : $"0x{value:X}";
}
