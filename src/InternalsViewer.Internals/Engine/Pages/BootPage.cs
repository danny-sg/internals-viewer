using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Boot Page
/// </summary>
public sealed class BootPage : Page
{
    public static readonly PageAddress BootPageAddress = new(1, 9);

    [DataStructureItem(ItemType.DatabaseName)]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Length of the database name in bytes
    /// </summary>
    /// <remarks>
    /// The name is held in a fixed 128 character field, this gives the used portion of it
    /// </remarks>
    [DataStructureItem(ItemType.DatabaseNameLength)]
    public int DatabaseNameLength { get; set; }

    [DataStructureItem(ItemType.DatabaseId)]
    public int DatabaseId { get; set; }

    [DataStructureItem(ItemType.CreatedVersion)]
    public short CreatedVersion { get; set; }

    [DataStructureItem(ItemType.CurrentVersion)]
    public short CurrentVersion { get; set; }

    /// <summary>
    /// First page for the Allocation Units (sys.sysallocunits) table
    /// </summary>
    /// <remarks>
    /// dbi_firstSysIndexes in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.FirstAllocationUnitsPage)]
    public PageAddress FirstAllocationUnitsPage { get; set; }

    [DataStructureItem(ItemType.Status)]
    public int Status { get; set; }

    /// <summary>
    /// Additional database status flags
    /// </summary>
    /// <remarks>
    /// dbi_relstat in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.ReleaseStatus)]
    public int ReleaseStatus { get; set; }

    [DataStructureItem(ItemType.CreatedDateTime)]
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// Date the database was last modified
    /// </summary>
    /// <remarks>
    /// dbi_modDate in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.ModifiedDateTime)]
    public DateTime ModifiedDateTime { get; set; }

    [DataStructureItem(ItemType.CompatabilityLevel)]
    public int CompatibilityLevel { get; set; }

    /// <summary>
    /// Last checkpoint LSN
    /// </summary>
    [DataStructureItem(ItemType.CheckpointLsn)]
    public LogSequenceNumber CheckpointLsn { get; set; }

    /// <summary>
    /// Oldest LSN that may have dirty pages in the buffer pool at the last checkpoint
    /// </summary>
    /// <remarks>
    /// dbi_DirtyPageLSN in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.DirtyPageLsn)]
    public LogSequenceNumber DirtyPageLsn { get; set; }

    /// <summary>
    /// LSN of the last versioning upgrade
    /// </summary>
    /// <remarks>
    /// dbi_latestVersioningUpgradeLSN in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.LatestVersioningUpgradeLsn)]
    public LogSequenceNumber LatestVersioningUpgradeLsn { get; set; }

    [DataStructureItem(ItemType.MaxLogSpaceUsed)]
    public long MaxLogSpaceUsed { get; set; }

    /// <summary>
    /// Highest timestamp value allocated in the database
    /// </summary>
    /// <remarks>
    /// dbi_maxDbTimestamp in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.MaxDatabaseTimestamp)]
    public long MaxDatabaseTimestamp { get; set; }

    /// <summary>
    /// Id of the last transaction to modify the database
    /// </summary>
    /// <remarks>
    /// dbi_lastxact in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.LastTransactionId)]
    public int LastTransactionId { get; set; }

    /// <summary>
    /// DBCC status flags
    /// </summary>
    /// <remarks>
    /// dbi_dbccFlags in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.DbccFlags)]
    public short DbccFlags { get; set; }

    [DataStructureItem(ItemType.Collation)]
    public int Collation { get; set; }

    [DataStructureItem(ItemType.NextAllocationUnitId)]
    public long NextAllocationUnitId { get; set; }

    /// <summary>
    /// Identifies the family of databases the database was restored or copied from
    /// </summary>
    /// <remarks>
    /// dbi_familyGUID in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.FamilyGuid)]
    public Guid FamilyGuid { get; set; }

    /// <summary>
    /// Current recovery fork, the first entry of the recovery fork name stack
    /// </summary>
    /// <remarks>
    /// dbi_recoveryForkNameStack in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.RecoveryForkGuid)]
    public Guid RecoveryForkGuid { get; set; }

    /// <summary>
    /// Service Broker identifier for the database
    /// </summary>
    /// <remarks>
    /// dbi_svcBrokerGUID in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.ServiceBrokerGuid)]
    public Guid ServiceBrokerGuid { get; set; }

    /// <summary>
    /// Service Broker option flags
    /// </summary>
    /// <remarks>
    /// dbi_svcBrokerOptions in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.ServiceBrokerOptions)]
    public int ServiceBrokerOptions { get; set; }

    /// <summary>
    /// Version of the engine that last upgraded the database
    /// </summary>
    /// <remarks>
    /// dbi_verRDB in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.ResourceDatabaseVersion)]
    public int ResourceDatabaseVersion { get; set; }

    /// <summary>
    /// Rowset id of the regular Persistent Version Store
    /// </summary>
    /// <remarks>
    /// dbi_pvsRowsetIdRegular in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.PersistentVersionStoreRowsetId)]
    public long PersistentVersionStoreRowsetId { get; set; }

    /// <summary>
    /// Rowset id of the long term Persistent Version Store
    /// </summary>
    /// <remarks>
    /// dbi_pvsRowsetIdLongTerm in the DBCC PAGE output
    /// </remarks>
    [DataStructureItem(ItemType.PersistentVersionStoreLongTermRowsetId)]
    public long PersistentVersionStoreLongTermRowsetId { get; set; }
}
