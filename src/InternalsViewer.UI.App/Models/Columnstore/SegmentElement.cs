namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// A named region of the segment blob, one per tab
/// </summary>
public sealed class SegmentElement
{
    public required string Name { get; init; }

    public required int Offset { get; init; }

    public required int Size { get; init; }

    public int EndOffset => Offset + Size;

    public string OffsetDescription => $"0x{Offset:X}";

}
