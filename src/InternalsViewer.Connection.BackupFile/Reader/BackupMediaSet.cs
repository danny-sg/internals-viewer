using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Format.Configuration;

namespace InternalsViewer.Connection.BackupFile.Reader;

/// <summary>
/// Media set is a collection of backup media that define the backup
/// </summary>
/// <remarks>
/// In the context of this project the media are always files
///
/// Backup media set = collection of .bak files that make up the full backup
/// </remarks>
internal sealed record BackupMediaSet(BackupConfiguration? Configuration, IReadOnlyList<BackupMediaFamily> Families);

/// <summary>
/// Backups created on a device in a media set constitute a media family
/// </summary>
/// <remarks>
/// Backup media family = .bak file
/// </remarks>
internal sealed record BackupMediaFamily(int FamilySequence, string Filename, IReadOnlyList<DescriptorBlock> Blocks);
