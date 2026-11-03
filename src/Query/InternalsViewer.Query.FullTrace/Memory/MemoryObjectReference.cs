namespace InternalsViewer.Query.FullTrace.Memory;

public sealed record MemoryObjectReference(ulong Address, string? Type, int PageSize)
{
    public string? Name => Type is { } type ? MemoryObjectNames.GetName(type) : null;

    private ulong PageMask => ~((ulong)PageSize - 1);

    public string GetText(ulong value)
        => value >= Address && (value & PageMask) == (Address & PageMask)
            ? $"0x{Address:X} + 0x{value - Address:X}"
            : $"0x{value:X}";
}
