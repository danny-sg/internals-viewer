using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for PFS (Page Free Space) pages
/// </summary>
public class PfsPageParser: PageParser, IPageParser<PfsPage>
{
    private const int PfsOffset = 100; // PFS byte array starts at offset 100
    private const int PfsSize = 8088; // PFS byte array is 8088 bytes/pages

    public PageType[] SupportedPageTypes => new[] { PageType.Pfs };

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public PfsPage Parse(PageData pageData)
    {
        var page = CopyToPageType<PfsPage>(pageData);

        var pfsBytes = GetPfsBytes(page);

        page.PfsBytes = pfsBytes;

        return page;
    }

    /// <summary>
    /// Loads the PFS bytes collection from the page data
    /// </summary>
    private static List<PfsByte> GetPfsBytes(PageData page)
    {
        var pfsData = new byte[PfsSize];

        Array.Copy(page.Data, PfsOffset, pfsData, 0, PfsSize);

        return pfsData.Select(PfsByteParser.Parse).ToList();
    }
}