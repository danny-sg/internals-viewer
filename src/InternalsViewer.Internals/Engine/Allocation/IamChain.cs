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
            _endExtents[i] = (page.StartPage.PageId + (AllocationPage.AllocationExtentInterval * 8)) / 8;
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
    /// Enumerates the object's footprint in the given file as contiguous page ranges, <c>From</c> .. <c>To</c> inclusive
    /// </summary>
    /// <remarks>
    /// The footprint at page resolution: each run of uniformly-allocated extents as one range (eight pages per extent,
    /// adjacent extents merged — so a large contiguous object is a handful of ranges, not thousands of pages), plus the
    /// single-page allocations off mixed extents and the IAM page itself as one-page ranges (a single page, not a whole
    /// extent). A small object living entirely in mixed extents has no uniform extents, so the single-page slots are
    /// what put it on the map. Bounded to the extent ranges the chain's IAM pages cover; costs O(object extents).
    /// Depends on <see cref="BuildLookup"/> having run.
    /// </remarks>
    public IEnumerable<(int From, int To)> GetAllocatedPageRanges(short fileId)
    {
        for (var i = 0; i < _pages.Length; i++)
        {
            var page = _pages[i];

            if (page.PageAddress.FileId == fileId)
            {
                yield return (page.PageAddress.PageId, page.PageAddress.PageId);
            }

            foreach (var slot in page.SinglePageSlots)
            {
                if (slot.FileId == fileId)
                {
                    yield return (slot.PageId, slot.PageId);
                }
            }

            if (page.StartPage.FileId != fileId)
            {
                continue;
            }

            var firstExtentPage = _startExtents[i] * 8;

            var map = page.AllocationMap;

            // Read the interval's bitmap directly (one bit per extent) rather than probing IsExtentAllocated per extent,
            // which would rescan every page each call. Coalesce runs of adjacent allocated extents into a single range.
            var runStart = -1;

            var runEndExclusive = -1;

            for (var relative = 0; relative < AllocationPage.AllocationExtentInterval; relative++)
            {
                if (((map[relative >> 3] >> (relative & 7)) & 1) == 0)
                {
                    continue;
                }

                var extentPage = firstExtentPage + relative * 8;

                if (extentPage == runEndExclusive)
                {
                    runEndExclusive += 8;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        yield return (runStart, runEndExclusive - 1);
                    }

                    runStart = extentPage;

                    runEndExclusive = extentPage + 8;
                }
            }

            if (runStart >= 0)
            {
                yield return (runStart, runEndExclusive - 1);
            }
        }
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
                var relIndex = extent - starts[i];

                return ((pages[i].AllocationMap[relIndex >> 3] >> (relIndex & 7)) & 1) != 0;
            }
        }

        return false;
    }
}