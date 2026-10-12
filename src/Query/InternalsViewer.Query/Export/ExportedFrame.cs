namespace InternalsViewer.Query.Export;

internal sealed record ExportedFrame
{
    public int Id { get; init; }

    public string Module { get; init; } = string.Empty;

    public ulong Address { get; init; }

    public string Pdb { get; init; } = string.Empty;

    public string Guid { get; init; } = string.Empty;

    public int Age { get; init; }

    public uint Rva { get; init; }

    public ulong Instance { get; init; }

    public string? Signature { get; init; }

    public string? Symbol { get; init; }

    public string? SymbolModule { get; init; }
}
