using InternalsViewer.Connection.BackupFile.Interfaces.Compression;
using ZstdSharp;

namespace InternalsViewer.Connection.BackupFile.Compression.Decoders;

/// <summary>
/// Decodes a ZSTD compressed backup block
/// </summary>
/// <remarks>
/// Each block payload is a complete, self contained ZSTD frame - single segment, with its content size declared
/// in the frame header. Nothing references output from an earlier block, so unlike Xpress a block can be decoded
/// on its own and no window has to be retained between blocks.
/// </remarks>
internal sealed class ZstdChunkDecoder : IChunkDecoder
{
    private static ReadOnlySpan<byte> FrameMagic => [0x28, 0xB5, 0x2F, 0xFD];

    private readonly Decompressor _decompressor = new();

    private byte[] _buffer = [];

    /// <summary>
    /// Frames are self-contained, so no previously produced output has to be kept
    /// </summary>
    public int MaximumMatchOffset => 0;

    public bool CanDecode(ReadOnlySpan<byte> payload)
        => payload.Length >= FrameMagic.Length && payload[..FrameMagic.Length].SequenceEqual(FrameMagic);

    public void Decode(ReadOnlyMemory<byte> payload, int uncompressedSize, SlidingWindowWriter output)
    {
        if (_buffer.Length < uncompressedSize)
        {
            _buffer = new byte[uncompressedSize];
        }

        int written;

        try
        {
            written = _decompressor.Unwrap(payload.Span, _buffer.AsSpan(0, uncompressedSize));
        }
        catch (ZstdException exception)
        {
            throw new InvalidDataException("Chunk payload is not a decodable ZSTD frame.", exception);
        }

        output.WriteRaw(_buffer.AsSpan(0, written));

        if (written < uncompressedSize)
        {
            output.WriteZeros(uncompressedSize - written);
        }
    }

    public void Dispose() => _decompressor.Dispose();
}
