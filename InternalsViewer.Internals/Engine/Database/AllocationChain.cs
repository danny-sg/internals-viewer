using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// An Allocation structure represented by a collection of allocation pages separated by an interval
/// </summary>
public class AllocationChain
{
    /// <summary>
    /// Check if a specific extent is allocated
    /// </summary>
    public virtual bool IsAllocated(int extent, short fileId)
    {
        return Pages[extent * 8 / Database.AllocationInterval].AllocationMap[extent % (Database.AllocationInterval / 8 + 1)];
    }

    /// <summary>
    /// Checks the allocation status or an extent
    /// </summary>
    public static bool CheckAllocationStatus(int targetExtent, short fileId, bool invert, AllocationChain chain)
    {
        return (!invert
                && chain.IsAllocated(targetExtent, fileId)
                && (fileId == chain.FileId || chain.IsMultiFile)
               )
               ||
               (invert
                && !chain.IsAllocated(targetExtent, fileId)
                && (fileId == chain.FileId || chain.IsMultiFile)
               );
    }

    public List<AllocationPage> Pages { get; } = new();

    public List<PageAddress> SinglePageSlots { get; set; } = new();

    public short FileId { get; set; }

    /// <summary>
    /// Determines if the Allocation spans multiple files
    /// </summary>
    public bool IsMultiFile { get; set; }
}