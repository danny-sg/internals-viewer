namespace InternalsViewer.Query.Callstack;

public sealed record CallstackFrame
{
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// The absolute virtual address of the frame — lets an unsymbolised frame be healed back to a module + RVA
    /// </summary>
    public ulong Address { get; set; }

    public string Pdb { get; set; } = string.Empty;

    public string Guid { get; set; } = string.Empty;

    public int Age { get; set; }

    public uint Rva { get; set; }

    public ResolvedCallstackFrame? Resolved { get; set; }
}