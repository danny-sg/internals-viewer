using InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Interfaces;

namespace InternalsViewer.Connection.BackupFile.Media;

/// <summary>
/// Backups created on a device in a media set constitute a media family
/// </summary>
/// <remarks>
/// Backup media family = .bak file
/// </remarks>
internal sealed record MediaFamily(int FamilySequence,
    string Filename,
    IContentSource Content,
    IReadOnlyList<DescriptorBlock> Blocks);