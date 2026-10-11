namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// A point in a compressed backup where decoding can be restarted
/// </summary>
internal sealed record Checkpoint(int ChunkIndex, long DecompressedOffset, byte[] History);
