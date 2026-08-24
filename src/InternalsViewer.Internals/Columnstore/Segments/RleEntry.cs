namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One run in a segment RLE array
/// </summary>
/// <remarks>
/// There are three types of runs:
///
///     Value greater than 0 - Value Run  - Gives the actual value
///     Value less than 0    - RLE Run    - Gives the index in the RLE array of the first bit packed value the run covers
///     Value equal to 0     - Terminator
/// </remarks>
public readonly record struct RleEntry(long Value, int Count)
{
    public bool IsBitpacked => Value < 0;

    public bool IsTerminator => Value == 0 && Count == 0;

    /// <summary>
    /// Index of the first bit packed value the run covers
    /// </summary>
    public int BitpackIndex => (int)(-Value - 1);
}
