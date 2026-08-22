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
                                  bool IsBitpacked,
                                  long Value,
                                  int Offset)
{
    public string ValueDescription => IsBitpacked ? $"Bit Pack Unit {Value}" : $"{Value}";
}
