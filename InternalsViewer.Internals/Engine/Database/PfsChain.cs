using System.Collections.Generic;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Engine.Database;

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
        return PfsPages[page / Database.PfsInterval].PfsBytes[page % Database.PfsInterval];
    }
}