using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.TransactionLog;

/// <summary>
/// Byte range of a page image changed by applying a log record
/// </summary>
/// <remarks>
/// Description is the full human-readable sentence (used for hex tooltips). A change to a known page field also
/// carries its ItemType and new Value, so consumers can name and style it from the shared marker styles rather
/// than the Description that repeats the region.
/// </remarks>
public sealed record ChangeSpan(int Offset, int Length, string Description)
{
    public ItemType? ItemType { get; init; }

    public string? Value { get; init; }
}
