using InternalsViewer.Connection.BackupFile.Mtf.Configuration;

namespace InternalsViewer.Connection.BackupFile.Media;

/// <summary>
/// Media set is a collection of backup media family (files) that define the backup
/// </summary>
/// <remarks>
/// In the context of this project the media family are always files
/// </remarks>
internal sealed record MediaSet(BackupConfiguration? Configuration, IReadOnlyList<MediaFamily> Families);