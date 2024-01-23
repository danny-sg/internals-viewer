using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// An Allocation structure represented by a collection of allocation pages
/// </summary>
public class AllocationChain : IAllocationChain<AllocationPage>
{
    public List<AllocationPage> Pages { get; } = new();

    /// <remarks>
    /// Allocation chains do not use the single page slots
    /// </remarks>
    public PageAddress[] SinglePageSlots => Array.Empty<PageAddress>();

    public short FileId { get; set; }

    /// <summary>
    /// Checks the allocation status or an extent
    /// </summary>
    public bool IsExtentAllocated(int targetExtent, short fileId, bool invert)
    {
        var value = IsExtentAllocated(targetExtent) && fileId == FileId;

        return invert ? !value : value;
    }

    /// <summary>
    /// Check if a specific extent is allocated
    /// </summary>
    private bool IsExtentAllocated(int extent)
    {
        // How many pages into the chain is the extent
        var pageIndex = extent / AllocationPage.AllocationInterval;

        // Bit index of the extent in the allocation (bit) map
        var extentIndex = extent % (AllocationPage.AllocationInterval + 1);

        if (pageIndex < 0 || pageIndex >= Pages.Count || extentIndex < 0 || extentIndex >= Pages[pageIndex].AllocationMap.Length)
        {
            throw new IndexOutOfRangeException("The extent is out of the range of the allocation chain.");
        }

        return Pages[pageIndex].AllocationMap[extentIndex];
    }
}