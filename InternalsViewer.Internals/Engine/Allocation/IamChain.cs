using InternalsViewer.Internals.Pages;
using System.Linq;

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
public class IamChain : AllocationChain
{
    public IamChain()
    {
        IsMultiFile = true;
    }

    /// <summary>
    /// Check is a specific extent is allocated
    /// </summary>
    public override bool IsAllocated(int extent, short fileId)
    {
        var page = Pages.FirstOrDefault(p => p.StartPage.FileId == fileId &&
                                             extent >= p.StartPage.PageId / 8 &&
                                             extent <= (p.StartPage.PageId + AllocationPage.AllocationInterval * 8) / 8);

        if (page == null)
        {
            return false;
        }

        return page.AllocationMap[extent - page.StartPage.Extent];
    }
}