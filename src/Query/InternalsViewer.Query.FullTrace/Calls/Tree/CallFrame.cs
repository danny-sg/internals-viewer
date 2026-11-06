namespace InternalsViewer.Query.FullTrace.Calls.Tree;

/// <summary>
/// Raw call stack frame
/// </summary>
public sealed record CallFrame
{
    public FrameModule Module { get; set; } = FrameModule.Empty;

    /// <summary>
    /// Absolute Address
    /// </summary>
    public ulong Address { get; set; }

    /// <summary>
    /// Relative Virtual Address
    /// </summary>
    public uint Rva { get; set; }

    public ulong InstanceAddress { get; set; }

    public FrameFunction? Function { get; set; }

    public uint? Offset { get; set; }

    public string QualifiedName
        => Function is { Name.Length: > 0 } function && !function.Name.StartsWith("0x", StringComparison.Ordinal)
            ? $"{Module.Name}!{function.Name}"
            : $"{Module.Name}+0x{Rva:X}";
}