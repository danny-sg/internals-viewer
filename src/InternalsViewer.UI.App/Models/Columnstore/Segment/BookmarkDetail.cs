namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

/// <summary>
/// One bookmark as the table lists it, the entry being where its position lands in the RLE array
/// </summary>
public sealed record BookmarkDetail(int Index, int Position, int Entry, int EndRow, int Offset,
                                    int EntryWidth, int EntryOffset)
{
    /// <summary>
    /// Working from the stored position to the entry, the position counting words rather than bytes or entries
    /// </summary>
    public ValueDerivation Derivation => new()
    {
        Steps =
        [
            new DerivationStep { Name = "Position", Value = $"{Position}" },
            new DerivationStep { Operator = "*", Name = "Word Size", Value = "4" },
            new DerivationStep { Operator = "/", Name = "Entry Width", Value = $"{EntryWidth}" }
        ],
        Result = $"{Entry}",
        Target = new SegmentNavigationTarget(SegmentRegion.RleArray, EntryOffset)
    };
}
