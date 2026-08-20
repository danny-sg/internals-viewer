using InternalsViewer.Connection.BackupFile.Interfaces.Compression;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.Connection.BackupFile.Compression.Decoders;

/// <summary>
/// Decodes one Xpress Huffman block of a compressed backup into the shared output window
/// </summary>
/// <remarks>
/// https://winprotocoldocs-bhdugrdyduf5h2e4.b02.azurefd.net/MS-XCA/%5bMS-XCA%5d.pdf
/// 
/// This is MS-XCA §2.2 decoding with the framing the backup container uses rather than the framing RtlDecompressBuffer expects, which
/// differs in two ways that matter:
///
///   1. Symbol 256 is an ordinary match (length nibble 0, offset bits 0), NOT an end of stream marker. Treating it as a terminator
///      truncates nearly every block.
///
///   2. A block ends when it has produced the number of bytes its header declares, NOT when the payload runs out - the payload is padded
///      to a word boundary, so input driven termination overshoots or falls short.
///
/// Buffers are reused across blocks - a backup contains thousands of them.
/// </remarks>
internal sealed class XpressHuffmanChunkDecoder : IChunkDecoder
{
    private const int MinimumTableSymbols = 8;

    private readonly XpressHuffmanDecoder _decoder = new();

    /// <summary>
    /// Xpress match offsets are at most 15 bits plus the implied bit, so 64 KB
    /// </summary>
    public int MaximumMatchOffset => XpressHuffmanDecoder.MaximumMatchOffset;

    /// <summary>
    /// Tests whether a payload begins with a canonical Huffman code length table
    /// </summary>
    /// <remarks>
    /// Code lengths of a canonical Huffman table satisfy Kraft equality - sum(2^-length) == 1 - which arbitrary
    /// bytes effectively never do.
    /// </remarks>
    public bool CanDecode(ReadOnlySpan<byte> payload) => XpressHuffmanDecoder.CanDecode(payload, MinimumTableSymbols);

    public void Decode(ReadOnlyMemory<byte> blockPayload, int uncompressedSize, SlidingWindowWriter output)
        => _decoder.Decode(blockPayload, uncompressedSize, output);

    public void Dispose()
    {
    }
}
