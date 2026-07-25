namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// A point in a compressed backup where decoding can be restarted
/// </summary>
/// <remarks>
/// Groundwork for reading pages without expanding the whole backup. Decoding can only restart at a block
/// boundary, and needs the preceding window because matches reach back across blocks, so a restart also has to
/// carry <see cref="Window"/>.
/// </remarks>
internal sealed record BackupCheckpoint(int BlockIndex, long DecompressedOffset, byte[] Window);
