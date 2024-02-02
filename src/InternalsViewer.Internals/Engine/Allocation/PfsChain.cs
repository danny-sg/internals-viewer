using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// PFS (Page Free Space) Page Chain
/// </summary>
/// <remarks> 
/// Collection of the PFS Pages in a database.
/// 
/// Each PFS page covers 8088 pages = 64MB meaning the number of PFS pages per file = (file size / 64MB)
/// 
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide"/>
/// </remarks>
public class PfsChain
{
    public List<PfsPage> PfsPages { get; } = new();

    /// <summary>
    /// Gets the PFS status for a given page
    /// </summary>
    public PfsByte GetPageStatus(int page)
    {
        // How many pages into the PFS chain is the page
        var pfsPageIndex = page / PfsPage.PfsInterval;

        // Where in the byte map is the page
        var pfsByteIndex = page % PfsPage.PfsInterval;

        // Check the PFS byte exists
        var result = PfsPages.Count > pfsPageIndex && PfsPages[pfsPageIndex].PfsBytes.Count > pfsByteIndex
                     ? PfsPages[pfsPageIndex].PfsBytes[pfsByteIndex]
                     : PfsByte.Unknown;

        return result;
    }
}