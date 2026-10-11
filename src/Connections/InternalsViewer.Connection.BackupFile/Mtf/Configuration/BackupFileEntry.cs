namespace InternalsViewer.Connection.BackupFile.Mtf.Configuration;

internal sealed record BackupFileEntry
{
    public int FileId { get; init; }

    public BackupFileType FileType { get; init; }

    public string LogicalName { get; init; } = string.Empty;

    public string PhysicalName { get; init; } = string.Empty;

    public long SizeInPages { get; init; }

    public long PhysicalSizeBytes { get; init; }

    public int FilegroupOrdinal { get; init; }
}
