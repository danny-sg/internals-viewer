using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Page Header
/// </summary>
public class Header: DataStructure
{
    public PageAddress PageAddress { get; set; }

    public PageAddress NextPage { get; set; }

    public PageAddress PreviousPage { get; set; }

    public PageType PageType { get; set; }

    public string PageTypeName => PageHelpers.GetPageTypeName(PageType);

    public long AllocationUnitId => IdHelpers.GetAllocationUnitId(ObjectId, IndexId);

    public int Level { get; set; }

    public int IndexId { get; set; }

    public int SlotCount { get; set; }

    public int FreeCount { get; set; }

    public int FreeData { get; set; }

    public int MinLen { get; set; }

    public int ReservedCount { get; set; }

    public int TransactionReservedCount { get; set; }

    public int TornBits { get; set; }

    public short FlagBits { get; set; }

    public int ObjectId { get; set; }

    public long PartitionId { get; set; }

    public LogSequenceNumber Lsn { get; set; }

    public byte HeaderVersion { get; set; }
    
    public short GhostRecordCount { get; set; }

    /// <summary>
    /// 0x80 = Data compressed
    /// 
    /// </summary>
    public byte TypeFlagBits { get; set; }
}