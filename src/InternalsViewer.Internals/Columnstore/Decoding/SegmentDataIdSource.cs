namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Which of a segment's stores a row's data id came out of
/// </summary>
public enum SegmentValueOrigin
{
    VariableLengthData,
    RleRun,
    BitPack
}

/// <summary>
/// A row's data id together with where in the segment it was read from
/// </summary>
/// <remarks>
/// The same segment reaches its data ids three different ways, and which one a row took decides both what the value
/// cost to store and what the working behind it looks like.
/// </remarks>
public readonly record struct SegmentDataIdSource(long DataId,
                                                  SegmentValueOrigin Origin,
                                                  int EntryIndex,
                                                  int SourceIndex);
