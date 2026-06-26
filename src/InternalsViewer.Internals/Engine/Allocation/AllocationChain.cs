using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// An Allocation structure represented by a collection of allocation pages
/// </summary>
public sealed class AllocationChain 
    : IAllocationPageChain<AllocationPage>
{
    public List<AllocationPage> Pages { get; } = [];

    /// <remarks>
    /// Allocation chains do not use the single page slots
    /// </remarks>
    public PageAddress[] SinglePageSlots => [];

    public short FileId { get; set; }

    /// <summary>
    /// Checks the allocation status of an extent
    /// </summary>
    public bool IsExtentAllocated(int targetExtent, short fileId, bool invert)
    {
        if (fileId != FileId)
        {
            return invert;
        }

        var allocated = IsExtentAllocated(targetExtent);

        return invert ? !allocated : allocated;
    }

    public bool AnyExtentsAllocated(int fromExtent, int toExtent, short fileId, bool isInverted)
    {
        for (var extent = fromExtent; extent <= toExtent; extent++)
        {
            if (IsExtentAllocated(extent) == isInverted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a specific extent is allocated
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsExtentAllocated(int extent)
    {
        var pageIndex = extent / AllocationPage.AllocationInterval;
        var extentIndex = extent % AllocationPage.AllocationInterval;

        if ((uint)pageIndex >= (uint)Pages.Count)
        {
            return false;
        }

        var map = Pages[pageIndex].AllocationMap;

        return (map[extentIndex >> 3] >> (extentIndex & 7) & 1) != 0;
    }
}