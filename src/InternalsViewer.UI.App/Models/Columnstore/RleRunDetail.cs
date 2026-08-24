using System;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

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
    public string ValueDescription => Address is { } address
        ? address.ToString()
        : IsValue ? $"{Value}" : $"Bit Pack Unit {Value}";

    /// <summary>
    /// What the map places on the hue wheel, a run addressing the store saying more by where its values sit
    /// </summary>
    public long ColourValue => StoreOrdinal >= 0 ? StoreOrdinal : Value;

    /// <summary>
    /// Where a run that covers a sequence has reached by its last row, which is where it stands for one value
    /// </summary>
    public long EndColourValue => IsValue ? ColourValue : ColourValue + Math.Max(0, Count - 1);
}
