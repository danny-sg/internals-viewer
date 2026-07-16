namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Raw call stack frame
/// </summary>
public sealed record CallstackFrame
{
    public string Module { get; set; } = string.Empty;

    public ulong Address { get; set; }

    public string Pdb { get; set; } = string.Empty;

    public string Guid { get; set; } = string.Empty;

    public int Age { get; set; }

    public uint Rva { get; set; }

    public ResolvedCallstackFrame? Resolved { get; set; }
}