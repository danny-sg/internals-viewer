using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Page Header
/// </summary>
public class PageHeader : DataStructure
{
    /// <summary>
    /// Type of page
    /// </summary>
    public PageType PageType { get; set; } = PageType.None;

    /// <summary>
    /// The page address in the format File Id : Page Id
    /// </summary>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// The next page if the page is part of a linked list
    /// </summary>
    public PageAddress NextPage { get; set; }

    /// <summary>
    /// The previous page if the page is part of a linked list
    /// </summary>
    public PageAddress PreviousPage { get; set; }

    public string PageTypeName => PageHelpers.GetPageTypeName(PageType);

    public long AllocationUnitId => IdHelpers.GetAllocationUnitId(ObjectId, IndexId);

    /// <summary>
    /// Object Id used to derive the Allocation Unit Id
    /// </summary>
    /// <remarks>
    /// Not necessarily the same as the object id of the table/index
    /// </remarks>
    public int ObjectId { get; set; }

    /// <summary>
    /// Index Id used to derive the Allocation Unit Id
    /// </summary>
    /// <remarks>
    /// Not necessarily the same as the index id in sys.indexes
    /// </remarks>
    public int IndexId { get; set; }

    /// <summary>
    /// Index Level
    /// </summary>
    /// <remarks>
    /// Level of the index in the B-Tree
    /// 
    /// Leaf level = 0
    /// Root level = > 0
    /// </remarks>
    public int Level { get; set; }

    /// <summary>
    /// Record count
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// Free space in bytes
    /// </summary>
    public int FreeCount { get; set; }

    public int FreeData { get; set; }

    /// <summary>
    /// Record fixed length size
    /// </summary>
    public int MinLen { get; set; }

    /// <summary>
    /// Space reserved by an active transaction
    /// </summary>
    public int ReservedCount { get; set; }

    /// <summary>
    /// Last transaction reserved count
    /// </summary>
    public int TransactionReservedCount { get; set; }

    public int TornBits { get; set; }

    /// <summary>
    /// Page detail flag bits
    /// </summary>
    /// <remarks>
    /// TODO: Work out what each bit means
    /// 
    /// 0x200 = Page checksum enabled
    /// 0x100 = Torn page detection enabled
    /// </remarks>
    public short FlagBits { get; set; }

    /// <summary>
    /// LSN (Log Sequence Number) of the last change to the page
    /// </summary>
    public LogSequenceNumber Lsn { get; set; }

    public byte HeaderVersion { get; set; }

    public short GhostRecordCount { get; set; }

    /// <summary>
    /// Page Type detail flag bits
    /// </summary>
    /// <remarks>
    /// TODO: Work out what each bit means, may be different per page type
    /// 
    /// 0x8 = Data compressed
    /// 0x4 = Fixed length fields
    /// 
    /// </remarks>
    public byte TypeFlagBits { get; set; }
}