namespace InternalsViewer.Query.Events.EventTypes;

public sealed record CallStackFrame
{
    public string Module { get; set; } = string.Empty;

    public string Pdb { get; set; } = string.Empty;

    public string Guid { get; set; } = string.Empty;

    public int Age { get; set; }

    public uint Rva { get; set; }

    public string ResolvedSymbol { get; set; } = string.Empty;
}