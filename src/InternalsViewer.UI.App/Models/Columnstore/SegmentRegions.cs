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
        SegmentRegion.Bookmarks => blob.Header.BookmarkArrayOffset,
        SegmentRegion.RleArray => blob.Header.RleArrayOffset,
        SegmentRegion.BitpackArray => blob.Header.IsVariableLengthData ? blob.Header.VariableLengthDataOffset : blob.Header.BitpackArrayOffset,
        SegmentRegion.VariableLengthData => blob.Header.VariableLengthDataOffset,
        _ => 0
    };

    public static SegmentRegion GetRegion(SegmentBlob blob, int offset)
    {
        // A store by value segment keeps its RLE array between the bookmarks and the store
        if (blob.Header.IsVariableLengthData)
        {
            return offset >= blob.Header.VariableLengthDataOffset ? SegmentRegion.VariableLengthData
                 : offset >= blob.Header.RleArrayOffset ? SegmentRegion.RleArray
                 : offset >= blob.Header.BookmarkArrayOffset ? SegmentRegion.Bookmarks
                 : SegmentRegion.Header;
        }

        return offset >= blob.Header.BitpackArrayOffset ? SegmentRegion.BitpackArray
             : offset >= blob.Header.RleArrayOffset ? SegmentRegion.RleArray
             : offset >= blob.Header.BookmarkArrayOffset ? SegmentRegion.Bookmarks
             : SegmentRegion.Header;
    }
}
