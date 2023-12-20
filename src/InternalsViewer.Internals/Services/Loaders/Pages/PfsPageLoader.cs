using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services.Loaders.Pages;

/// <summary>
/// Service responsible for loading PFS pages
/// </summary>
public class PfsPageLoader(IPageLoader pageLoader) : IPfsPageLoader
{
    private const int PfsOffset = 100; // PFS byte array starts at offset 100
    private const int PfsSize = 8088; // PFS byte array is 8088 bytes/pages

    public IPageLoader PageLoader { get; } = pageLoader;

    public async Task<PfsPage> Load(DatabaseDetail databaseDetail, PageAddress pageAddress)
    {
        var page = await PageLoader.Load<PfsPage>(databaseDetail, pageAddress);

        if (page.PageHeader.PageType != PageType.Pfs)
        {
            throw new InvalidOperationException("Page type is not PFS");
        }

        var pfsBytes = GetPfsBytes(page);

        page.PfsBytes = pfsBytes;

        return page;
    }

    /// <summary>
    /// Loads the PFS bytes collection from the page data
    /// </summary>
    private static List<PfsByte> GetPfsBytes(Page page)
    {
        var pfsData = new byte[PfsSize];

        Array.Copy(page.PageData, PfsOffset, pfsData, 0, PfsSize);

        return pfsData.Select(PfsByteParser.Parse).ToList();
    }
}