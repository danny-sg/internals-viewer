using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// An Allocation structure represented by a collection of allocation pages
/// </summary>
public class AllocationChain
{
    /// <summary>
    /// Check if a specific extent is allocated
    /// </summary>
    public virtual bool IsAllocated(int extent, short fileId)
    {
        // How many pages into the chain is the extent
        var pageIndex = extent / AllocationPage.AllocationInterval;

        // Bit index of the extent in the allocation (bit) map
        var extentIndex = extent % (AllocationPage.AllocationInterval + 1);

        return Pages[pageIndex].AllocationMap[extentIndex];
    }

    /// <summary>
    /// Checks the allocation status or an extent
    /// </summary>
    public static bool GetAllocatedStatus(int targetExtent, short fileId, bool invert, AllocationChain chain)
    {
        var value = chain.IsAllocated(targetExtent, fileId) && (fileId == chain.FileId || chain.IsMultiFile);

        return invert ? !value : value;
    }

    public List<AllocationPage> Pages { get; } = new();

    public List<PageAddress> SinglePageSlots { get; set; } = new();

    public short FileId { get; set; }

    /// <summary>
    /// Determines if the Allocation spans multiple files
    /// </summary>
    public bool IsMultiFile { get; set; }
}