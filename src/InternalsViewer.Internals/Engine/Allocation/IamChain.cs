using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// IAM (Index Allocation Map) Structure
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide
/// 
/// Terminology:
/// 
///     IAM            - Index Allocation Map - allocation map for a single object/allocation unit
///     IAM Chain      - Linked list of IAM pages
///     Uniform Extent - 8 contiguous pages covering 64KB
///     Mixed Extent   - An extent that contains pages from multiple objects
///     GAM Interval   - Interval between allocation pages
/// 
/// An IAM represents the allocation for an allocation unit. An IAM page has a standard 96-byte header, a bitmap covering 64,000 extents 
/// and eight single page slots. The bitmap index represents the extent location. If a bit is set to 1 the extent is allocated to the 
/// object.
/// 
/// Single page slots are used when an object has a small amount of data and does not require a full extent. SQL Server 2016+ defaults to
/// uniform extents for user databases.
/// 
/// If the allocation unit spans more than 64,000 extents additional IAM pages are linked via the page header Next Page and Previous Page 
/// pointers to create a chain.
/// </remarks>
public sealed class IamChain : IAllocationPageChain<IamPage>
{
    private IamPage[] _pages = [];

    private int[] _startExtents = [];
    
    private int[] _endExtents = [];

    public List<IamPage> Pages { get; } = new();

    public PageAddress[] SinglePageSlots { get; set; } = [];

    /// <summary>
    /// Builds the precomputed extent range lookup arrays. Call after all pages have been added.
    /// </summary>
    public void BuildLookup()
    {
        var pages = Pages;
        var count = pages.Count;

        _pages = new IamPage[count];
        _startExtents = new int[count];
        _endExtents = new int[count];

        for (var i = 0; i < count; i++)
        {
            var page = pages[i];
            _pages[i] = page;
            _startExtents[i] = page.StartPage.PageId / 8;
            _endExtents[i] = (page.StartPage.PageId + AllocationPage.AllocationExtentInterval * 8) / 8;
        }
    }

    /// <summary>
    /// Checks the allocation status of an extent
    /// </summary>
    public bool IsExtentAllocated(int targetExtent, short fileId, bool invert)
    {
        var value = IsExtentAllocated(targetExtent, fileId);

        return invert ? !value : value;
    }

    public bool AnyExtentsAllocated(int fromExtent, int toExtent, short fileId, bool isInverted)
    {
        for (var extent = fromExtent; extent <= toExtent; extent++)
        {
            if (IsExtentAllocated(extent, fileId) == isInverted)
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
    private bool IsExtentAllocated(int extent, short fileId)
    {
        var pages = _pages;
        var starts = _startExtents;
        var ends = _endExtents;

        for (var i = 0; i < pages.Length; i++)
        {
            if (pages[i].StartPage.FileId == fileId && extent >= starts[i] && extent <= ends[i])
            {
                var relIndex = extent - pages[i].StartPage.Extent;

                return (pages[i].AllocationMap[relIndex >> 3] >> (relIndex & 7) & 1) != 0;
            }
        }

        return false;
    }
}