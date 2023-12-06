using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Page Header
/// </summary>
public class Header
{
    /// <summary>
    /// Gets or sets the page address.
    /// </summary>
    public PageAddress PageAddress { get; set; }

    /// <summary>
    /// Gets or sets the next page.
    /// </summary>
    public PageAddress NextPage { get; set; }

    /// <summary>
    /// Gets or sets the previous page.
    /// </summary>
    public PageAddress PreviousPage { get; set; }

    /// <summary>
    /// Gets or sets the type of the page.
    /// </summary>
    public PageType PageType { get; set; }

    /// <summary>
    /// Gets or sets the allocation unit id.
    /// </summary>
    public long AllocationUnitId { get; set; }

    /// <summary>
    /// Gets or sets the page index level.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the index id.
    /// </summary>
    public int IndexId { get; set; }

    /// <summary>
    /// Gets or sets the slot count.
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// Gets or sets the free count value.
    /// </summary>
    public int FreeCount { get; set; }

    /// <summary>
    /// Gets or sets the free data value.
    /// </summary>
    public int FreeData { get; set; }

    /// <summary>
    /// Gets or sets the min len value.
    /// </summary>
    public int MinLen { get; set; }

    /// <summary>
    /// Gets or sets the reserved count value.
    /// </summary>
    public int ReservedCount { get; set; }

    /// <summary>
    /// Gets or sets the xact reserved count value.
    /// </summary>
    public int XactReservedCount { get; set; }

    /// <summary>
    /// Gets or sets the torn bits value.
    /// </summary>
    public long TornBits { get; set; }

    /// <summary>
    /// Gets or sets the flag bits.
    /// </summary>
    public string FlagBits { get; set; }

    /// <summary>
    /// Gets or sets the object id.
    /// </summary>
    public long ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the partition id.
    /// </summary>
    public long PartitionId { get; set; }

    /// <summary>
    /// Gets or sets the LSN.
    /// </summary>
    public LogSequenceNumber Lsn { get; set; }

    /// <summary>
    /// Gets or sets the allocation unit.
    /// </summary>
    public string AllocationUnit { get; set; }

    /// <summary>
    /// Gets or sets the name of the page type.
    /// </summary>
    public string PageTypeName { get; set; }
}