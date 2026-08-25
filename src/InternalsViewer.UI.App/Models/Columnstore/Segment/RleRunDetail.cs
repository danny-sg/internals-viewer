using System;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// One run of the RLE array as the map draws it, its width being the rows it covers
/// </summary>
/// <remarks>
/// A literal run carries its value, a bit packed one the index of the unit its values start at. The map colours by
/// whichever of the two the run holds, so the colour says something about the data rather than only about the kind.
/// </remarks>
public sealed record RleRunDetail(int Index,
                                  int StartRow,
                                  int Count,
                                  bool IsValue,
                                  long Value,
                                  int Offset,
                                  SegmentPageSlot? Address = null,
                                  int StoreOrdinal = -1)
{
    /// <summary>
    /// What the run names, being a place in the value store or a number, the run type saying which
    /// </summary>
    public string ValueDescription => Address is { } address ? address.ToString() : $"{Value}";

    /// <summary>
    /// What the run does at the place it names, which the sign of the value decides
    /// </summary>
    public string RunType => IsTerminator ? "Terminator" : IsValue ? "Repeat" : "Read";

    /// <summary>
    /// What the value names, an address into the store or a place in the bit pack array or the value itself
    /// </summary>
    public string ValueType => IsTerminator
        ? string.Empty
        : Address is not null ? "Page Slot" : IsValue ? "Value" : "Bit Pack Entry";

    /// <summary>
    /// Whether the value names somewhere else, which is what makes it worth following
    /// </summary>
    public bool IsLink => Address is not null || (!IsValue && !IsTerminator);

    /// <summary>
    /// What the map places on the hue wheel, a run addressing the store saying more by where its values sit
    /// </summary>
    public long ColourValue => StoreOrdinal >= 0 ? StoreOrdinal : Value;

    /// <summary>
    /// Where a run that covers a sequence has reached by its last row, which is where it stands for one value
    /// </summary>
    public long EndColourValue => IsValue ? ColourValue : ColourValue + Math.Max(0, Count - 1);

    private bool IsTerminator => Count == 0 && Value == 0;
}
