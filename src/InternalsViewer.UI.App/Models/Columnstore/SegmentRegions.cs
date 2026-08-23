using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// Maps between a region of the blob and the offsets it occupies
/// </summary>
/// <remarks>
/// The regions run back to back in a fixed order, so a region is found by taking the last one that starts at or
/// before the offset. Testing them in reverse also keeps an empty region, which shares its start with the region
/// after it, from claiming that region's offsets.
/// </remarks>
public static class SegmentRegions
{
    public static int GetOffset(SegmentBlob blob, SegmentRegion region) => region switch
    {
        SegmentRegion.Bookmarks => blob.BookmarkArrayOffset,
        SegmentRegion.RleArray => blob.IsStoreByValue ? blob.VariableLengthDataOffset : blob.RleArrayOffset,
        SegmentRegion.BitpackArray => blob.IsStoreByValue ? blob.VariableLengthDataOffset : blob.BitpackArrayOffset,
        SegmentRegion.VariableLengthData => blob.VariableLengthDataOffset,
        _ => 0
    };

    public static SegmentRegion GetRegion(SegmentBlob blob, int offset)
    {
        if (blob.IsStoreByValue)
        {
            return offset >= blob.VariableLengthDataOffset ? SegmentRegion.VariableLengthData
                 : offset >= blob.BookmarkArrayOffset ? SegmentRegion.Bookmarks
                 : SegmentRegion.Header;
        }

        return offset >= blob.BitpackArrayOffset ? SegmentRegion.BitpackArray
             : offset >= blob.RleArrayOffset ? SegmentRegion.RleArray
             : offset >= blob.BookmarkArrayOffset ? SegmentRegion.Bookmarks
             : SegmentRegion.Header;
    }
}
