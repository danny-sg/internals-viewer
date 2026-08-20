namespace InternalsViewer.Internals.Compression;

/// <summary>
/// The code a canonical Huffman table assigns to one symbol
/// </summary>
public readonly record struct HuffmanCode(int Symbol, int BitLength, int Code)
{
    /// <summary>
    /// The code as it appears in the stream, leading zeros being part of it rather than padding
    /// </summary>
    public string Bits => Convert.ToString(Code, 2).PadLeft(BitLength, '0');

    /// <summary>
    /// Whether a bit of the code branches to the one side or the other, counted from the first bit read
    /// </summary>
    public bool IsSet(int bitIndex) => (Code & (1 << (BitLength - bitIndex - 1))) != 0;
}
