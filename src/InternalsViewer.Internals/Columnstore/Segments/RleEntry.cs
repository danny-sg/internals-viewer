namespace InternalsViewer.Internals.Columnstore.Segments;

/// <summary>
/// One run in a segment RLE array, either a repeated data id or a reference into the bit pack array
/// </summary>
public readonly record struct RleEntry(int Value, int Count)
{
    public bool IsBitpacked => Value < 0;

    public bool IsTerminator => Value == 0 && Count == 0;

    /// <summary>
    /// Index of the first bit packed value the run covers
    /// </summary>
    public int BitpackIndex => -Value - 1;
}
