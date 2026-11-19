using InternalsViewer.Query.FullTrace.Memory.Layouts.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public sealed record MemoryField(string Name, int Offset, int Length, bool IsHex = false)
{
    private const char AnySuffix = '*';

    public IReadOnlyList<string> Writers { get; init; } = [];

    public bool IsNumber { get; init; }

    public string? Structure { get; init; }

    public string? Target { get; init; }

    public bool IsFunction { get; init; }

    public bool IsBytes { get; init; }

    public MemoryFieldDecoder? Decoder { get; init; }

    public bool IsPointer => Length == sizeof(ulong) && !IsNumber;

    public bool IsWrittenBy(string writer)
        => Writers.Any(w => w[^1] == AnySuffix ? writer.StartsWith(w[..^1], StringComparison.Ordinal) : writer == w);
}
