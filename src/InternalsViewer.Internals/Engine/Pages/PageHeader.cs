using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Helpers;
using System.Drawing;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Page Header
/// </summary>
public class PageHeader : DataStructure
{
    /// <summary>
    /// Page Header is always 96 bytes
    /// </summary>
    public static readonly short Size = 96;

    /// <summary>
    /// Type of page
    /// </summary>
    [DataStructureItem(DataStructureItemType.PageType)]
    public PageType PageType { get; set; } = PageType.None;

    /// <summary>
    /// The page address in the format File Id : Page Id
    /// </summary>
    [DataStructureItem(DataStructureItemType.HeaderPageAddress)]
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// The next page if the page is part of a linked list
    /// </summary>
    [DataStructureItem(DataStructureItemType.NextPage)]
    public PageAddress NextPage { get; set; }

    /// <summary>
    /// The previous page if the page is part of a linked list
    /// </summary>
    [DataStructureItem(DataStructureItemType.PreviousPage)]
    public PageAddress PreviousPage { get; set; }

    public string PageTypeName => PageHelpers.GetPageTypeName(PageType);

    public long AllocationUnitId => IdHelpers.GetAllocationUnitId(InternalObjectId, InternalIndexId);

    /// <summary>
    /// Object Id used to derive the Allocation Unit Id
    /// </summary>
    /// <remarks>
    /// Not necessarily the same as the object id of the table/index
    /// </remarks>
    [DataStructureItem(DataStructureItemType.InternalObjectId)]
    public int InternalObjectId { get; set; }

    /// <summary>
    /// Index Id used to derive the Allocation Unit Id
    /// </summary>
    /// <remarks>
    /// Not necessarily the same as the index id in sys.indexes
    /// </remarks>
    [DataStructureItem(DataStructureItemType.InternalIndexId)]
    public int InternalIndexId { get; set; }

    /// <summary>
    /// Index Level
    /// </summary>
    /// <remarks>
    /// Level of the index in the B-Tree
    /// 
    /// Leaf level = 0
    /// Root level = > 0
    /// </remarks>
    [DataStructureItem(DataStructureItemType.IndexLevel)]
    public int Level { get; set; }

    /// <summary>
    /// Record/slot count
    /// </summary>
    [DataStructureItem(DataStructureItemType.PageSlotCount)]
    public int SlotCount { get; set; }

    /// <summary>
    /// Free space in bytes
    /// </summary>
    [DataStructureItem(DataStructureItemType.FreeCount)]
    public int FreeCount { get; set; }

    [DataStructureItem(DataStructureItemType.FreeData)]
    public int FreeData { get; set; }

    /// <summary>
    /// Record fixed length size
    /// </summary>
    [DataStructureItem(DataStructureItemType.FixedLengthSize)]
    public int FixedLengthSize { get; set; }

    /// <summary>
    /// Space reserved by an active transaction
    /// </summary>
    [DataStructureItem(DataStructureItemType.ReservedCount)]
    public int ReservedCount { get; set; }

    /// <summary>
    /// Last transaction reserved count
    /// </summary>
    [DataStructureItem(DataStructureItemType.TransactionReservedCount)]
    public int TransactionReservedCount { get; set; }

    [DataStructureItem(DataStructureItemType.TornBits)]
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
    [DataStructureItem(DataStructureItemType.FlagBits)]
    public short FlagBits { get; set; }

    /// <summary>
    /// LSN (Log Sequence Number) of the last change to the page
    /// </summary>
    [DataStructureItem(DataStructureItemType.Lsn)]
    public LogSequenceNumber Lsn { get; set; }

    [DataStructureItem(DataStructureItemType.HeaderVersion)]
    public byte HeaderVersion { get; set; }

    [DataStructureItem(DataStructureItemType.GhostRecordCount)]
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
    [DataStructureItem(DataStructureItemType.TypeFlagBits)]
    public byte TypeFlagBits { get; set; }

    [DataStructureItem(DataStructureItemType.InternalTransactionId)]
    public PageAddress InternalTransactionId { get; set; }
}