namespace InternalsViewer.Connection.BackupFile.Content;

/// <summary>
/// Random access to the MTF backup stream, whichever way it is physically stored
/// </summary>
/// <remarks>
/// Offsets are always positions in the logical MTF stream, never positions in the file on disk. For an
/// uncompressed backup the two are the same. For a compressed backup the implementation resolves the offset to
/// the block holding it and decodes only what is needed.
///
/// Implementations must allow concurrent reads - allocation unit IAM chains are loaded in parallel.
/// </remarks>
internal interface IBackupContentSource : IDisposable
{
    long Length { get; }

    void Read(long offset, Span<byte> buffer);
}
