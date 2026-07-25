using InternalsViewer.Connection.BackupFile.Interfaces;

namespace InternalsViewer.Connection.BackupFile.Media;

/// <summary>
/// A backup file paired with the content source that presents its MTF stream
/// </summary>
/// <remarks>
/// The filename is carried alongside for diagnostics only - nothing reads the file directly, all access goes
/// through the content source so compressed and uncompressed backups are handled identically.
/// </remarks>
internal sealed record MediaSource(string Filename, IContentSource Content);
