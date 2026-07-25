using InternalsViewer.Connection.BackupFile.Compression;

namespace InternalsViewer.Connection.BackupFile.Interfaces.Compression;

/// <summary>
/// Decodes the payload of one compressed backup block
/// </summary>
/// <remarks>
/// The container - block headers, chaining, raw blocks, checkpoints - is the same whichever algorithm was used,
/// so only the payload codec varies. Everything an algorithm knows about its own payload belongs here rather
/// than in the walker or the consumers.
/// </remarks>
internal interface IChunkDecoder : IDisposable
{
    /// <summary>
    /// Furthest back a match can reference previously produced output
    /// </summary>
    /// <remarks>
    /// Sizes the window that has to be retained while decoding. Too small and matches silently read the wrong
    /// bytes, so this is the algorithm's own limit, not a container constant.
    /// </remarks>
    int MaximumMatchOffset { get; }

    /// <summary>
    /// Whether this decoder recognises the payload as its own
    /// </summary>
    /// <remarks>
    /// Blocks inside a FILESTREAM section carry payloads that are not a compressed stream at all, and those have
    /// to be skipped rather than decoded. This is also what tells a trustworthy resync point from a coincidental
    /// block marker.
    /// </remarks>
    bool CanDecode(ReadOnlySpan<byte> payload);

    void Decode(ReadOnlyMemory<byte> payload, int uncompressedSize, SlidingWindowWriter output);
}
