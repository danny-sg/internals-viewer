using InternalsViewer.Connection.BackupFile.Content;

namespace InternalsViewer.Connection.BackupFile.Reader;

/// <summary>
/// A backup file paired with the content source that presents its MTF stream
/// </summary>
/// <remarks>
/// The filename is carried alongside for diagnostics only - nothing reads the file directly, all access goes
/// through the content source so compressed and uncompressed backups are handled identically.
/// </remarks>
internal sealed record BackupMediaSource(string Filename, IBackupContentSource Content);
