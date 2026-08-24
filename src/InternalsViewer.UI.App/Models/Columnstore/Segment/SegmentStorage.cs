using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.UI.App.Models.Columnstore.Segment;

public enum SegmentStorage
{
    Unknown,
    RunLength,
    BitPack,
    Mixed,
    VariableLengthData
}

public static class SegmentStorageExtensions
{
    /// <summary>
    /// Which of the segment's stores the rows came out of, taken from the prologue alone
    /// </summary>
    /// <remarks>
    /// A run length segment always carries an RLE array, so the array's presence says nothing. What separates the
    /// cases is whether it holds anything beyond the one entry pointing at the bit pack array and the terminator.
    /// </remarks>
    public static SegmentStorage Classify(SegmentBlobHeader? header)
    {
        if (header is null)
        {
            return SegmentStorage.Unknown;
        }

        if (header.IsVariableLengthData)
        {
            return SegmentStorage.VariableLengthData;
        }

        if (!header.HasBitpackArray)
        {
            return SegmentStorage.RunLength;
        }

        return header.RleEntryCount > 2 ? SegmentStorage.Mixed : SegmentStorage.BitPack;
    }

    public static string Describe(this SegmentStorage storage) => storage switch
    {
        SegmentStorage.RunLength => "RLE",
        SegmentStorage.BitPack => "Bit Pack",
        SegmentStorage.Mixed => "RLE + Bit Pack",
        SegmentStorage.VariableLengthData => "Variable Length Data",
        _ => string.Empty
    };
}
