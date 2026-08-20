using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// File Header Page
/// </summary>
/// <remarks>
/// Holds a single record describing the database file, the same properties DBCC FILEHEADER reports
/// </remarks>
public sealed class FileHeaderPage : DataPage
{
    /// <summary>
    /// Logical name of the file
    /// </summary>
    [DataStructureItem(ItemType.LogicalName)]
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// Identifies the file within the database family it was restored or copied from
    /// </summary>
    [DataStructureItem(ItemType.BindingId)]
    public Guid BindingId { get; set; }

    /// <summary>
    /// Unique identifier for the file
    /// </summary>
    [DataStructureItem(ItemType.FileIdGuid)]
    public Guid FileIdGuid { get; set; }

    /// <summary>
    /// File Id within the database
    /// </summary>
    /// <remarks>
    /// FileIdProp in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.FileId)]
    public short FileId { get; set; }

    /// <summary>
    /// File group the file belongs to
    /// </summary>
    [DataStructureItem(ItemType.FileGroupId)]
    public short FileGroupId { get; set; }

    /// <summary>
    /// Current size of the file in pages
    /// </summary>
    [DataStructureItem(ItemType.FileSize)]
    public int FileSize { get; set; }

    /// <summary>
    /// Maximum size the file can grow to in pages, -1 for unlimited
    /// </summary>
    [DataStructureItem(ItemType.MaxSize)]
    public int MaxSize { get; set; }

    /// <summary>
    /// Smallest size the file can be shrunk to in pages
    /// </summary>
    [DataStructureItem(ItemType.MinSize)]
    public int MinSize { get; set; }

    /// <summary>
    /// Size a user initiated shrink will target in pages, -1 if none is set
    /// </summary>
    [DataStructureItem(ItemType.UserShrinkSize)]
    public int UserShrinkSize { get; set; }

    /// <summary>
    /// Growth increment, in pages or as a percentage depending on the file status
    /// </summary>
    [DataStructureItem(ItemType.Growth)]
    public int Growth { get; set; }

    [DataStructureItem(ItemType.Perf)]
    public int Perf { get; set; }

    /// <summary>
    /// File status flags
    /// </summary>
    [DataStructureItem(ItemType.FileStatus)]
    public int Status { get; set; }

    /// <summary>
    /// Sector size of the volume the file was created on
    /// </summary>
    [DataStructureItem(ItemType.SectorSize)]
    public int SectorSize { get; set; }

    /// <summary>
    /// LSN of the last backup
    /// </summary>
    [DataStructureItem(ItemType.BackupLsn)]
    public LogSequenceNumber BackupLsn { get; set; }

    /// <summary>
    /// LSN of the first update to the file
    /// </summary>
    [DataStructureItem(ItemType.FirstUpdateLsn)]
    public LogSequenceNumber FirstUpdateLsn { get; set; }

    /// <summary>
    /// LSN of the oldest restored page
    /// </summary>
    [DataStructureItem(ItemType.OldestRestoredLsn)]
    public LogSequenceNumber OldestRestoredLsn { get; set; }

    /// <summary>
    /// Highest LSN applied to the file
    /// </summary>
    [DataStructureItem(ItemType.MaxLsn)]
    public LogSequenceNumber MaxLsn { get; set; }

    [DataStructureItem(ItemType.FirstLsn)]
    public LogSequenceNumber FirstLsn { get; set; }

    /// <summary>
    /// LSN the file was created at
    /// </summary>
    [DataStructureItem(ItemType.CreateLsn)]
    public LogSequenceNumber CreateLsn { get; set; }

    /// <summary>
    /// LSN of the differential base, the point differential backups are taken from
    /// </summary>
    [DataStructureItem(ItemType.DifferentialBaseLsn)]
    public LogSequenceNumber DifferentialBaseLsn { get; set; }

    /// <summary>
    /// Identifies the base backup differential backups are taken from
    /// </summary>
    [DataStructureItem(ItemType.DifferentialBaseGuid)]
    public Guid DifferentialBaseGuid { get; set; }

    /// <summary>
    /// LSN the file was taken offline at
    /// </summary>
    [DataStructureItem(ItemType.FileOfflineLsn)]
    public LogSequenceNumber FileOfflineLsn { get; set; }

    /// <summary>
    /// Restore status flags
    /// </summary>
    [DataStructureItem(ItemType.RestoreStatus)]
    public int RestoreStatus { get; set; }

    /// <summary>
    /// LSN redo starts from during a restore
    /// </summary>
    [DataStructureItem(ItemType.RestoreRedoStartLsn)]
    public LogSequenceNumber RestoreRedoStartLsn { get; set; }
}
