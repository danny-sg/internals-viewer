namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// One symbol read while decoding an entry, positioned by the bits it was read from
/// </summary>
/// <remarks>
/// A code is only as long as the symbol is common, so the steps of one entry vary in width. The first step or two
/// carry the entry length rather than its content.
/// </remarks>
public readonly record struct HuffmanDecodeStep(int BitOffset, int BitLength, int Symbol, int Code, bool IsLength)
{
    public string Bits => Convert.ToString(Code, 2).PadLeft(BitLength, '0');

    /// <summary>
    /// The character the symbol prints as, blank where it has none rather than showing a control code
    /// </summary>
    public string Character => Symbol is >= 0x20 and < 0x7F ? ((char)Symbol).ToString() : string.Empty;
}
