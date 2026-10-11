namespace InternalsViewer.Connection.BackupFile.Compression.Chunks;

/// <summary>
/// Located compressed chunk, with the payload extent resolved
/// </summary>
/// <remarks>
/// A raw chunk does not carry a usable length - its payload starts at the next 512 byte boundary and runs to the next chunk. The walker
/// resolves that length once so consumers never have to derive it.
/// length.
/// </remarks>
internal readonly record struct ChunkLocation(long Offset,
                                              ChunkHeader Header,
                                              long PayloadOffset,
                                              int PayloadLength,
                                              int UncompressedSize);
