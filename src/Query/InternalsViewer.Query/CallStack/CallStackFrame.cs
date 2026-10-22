namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Raw call stack frame
/// </summary>
public sealed record CallstackFrame
{
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Absolute Address
    /// </summary>
    public ulong Address { get; set; }

    /// <summary>
    /// PDB Path
    /// </summary>
    public string Pdb { get; set; } = string.Empty;

    public string Guid { get; set; } = string.Empty;

    public int Age { get; set; }

    /// <summary>
    /// Relative Virtual Address
    /// </summary>
    public uint Rva { get; set; }

    public ulong InstanceAddress { get; set; }

    public string? Signature { get; set; }

    public ResolvedCallstackFrame? Resolved { get; set; }
}