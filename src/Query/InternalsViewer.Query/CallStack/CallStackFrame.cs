namespace InternalsViewer.Query.CallStack;

/// <summary>
/// Raw call stack frame
/// </summary>
public sealed record CallstackFrame
{
    public CallstackModule Module { get; set; } = CallstackModule.Empty;

    /// <summary>
    /// Absolute Address
    /// </summary>
    public ulong Address { get; set; }

    /// <summary>
    /// Relative Virtual Address
    /// </summary>
    public uint Rva { get; set; }

    public ulong InstanceAddress { get; set; }

    public CallstackFunction? Function { get; set; }

    public uint? Offset { get; set; }
}