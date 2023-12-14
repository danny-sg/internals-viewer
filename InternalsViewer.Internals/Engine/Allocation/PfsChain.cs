using System.Collections.Generic;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Allocation;

/// <summary>
/// PFS (Page Free Space) Page
/// </summary>
/// <remarks> 
///     <see href="https://learn.microsoft.com/en-us/sql/relational-databases/pages-and-extents-architecture-guide"/>
/// </remarks>
public class PfsChain
{
    public List<PfsPage> PfsPages { get; } = new();

    public PfsByte GetPagePfsStatus(int page)
    {
        // How many pages into the PFS chain is the page
        var pfsPageIndex = page / PfsPage.PfsInterval;

        // Where in the byte map is the page
        var pfsByteIndex = page % PfsPage.PfsInterval;

        return PfsPages[pfsPageIndex].PfsBytes[pfsByteIndex];
    }
}