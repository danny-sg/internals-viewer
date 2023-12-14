using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Page Header
/// </summary>
public record Header
{
    public PageAddress PageAddress { get; set; }

    public PageAddress NextPage { get; set; }

    public PageAddress PreviousPage { get; set; }

    public PageType PageType { get; set; }

    public string PageTypeName => PageHelpers.GetPageTypeName(PageType);

    public long AllocationUnitId { get; set; }

    public int Level { get; set; }

    public int IndexId { get; set; }

    public int SlotCount { get; set; }

    public int FreeCount { get; set; }

    public int FreeData { get; set; }

    public int MinLen { get; set; }

    public int ReservedCount { get; set; }

    public int XactReservedCount { get; set; }

    public long TornBits { get; set; }

    public string FlagBits { get; set; } = string.Empty;

    public long ObjectId { get; set; }

    public long PartitionId { get; set; }

    public LogSequenceNumber Lsn { get; set; }

    public string AllocationUnit { get; set; } = string.Empty;
}