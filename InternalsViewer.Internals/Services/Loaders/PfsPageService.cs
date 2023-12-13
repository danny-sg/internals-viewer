using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders;

/// <summary>
/// Service responsible for loading PFS pages
/// </summary>
public class PfsPageService(IPageService pageService) : IPfsPageService
{
    private const int PfsOffset = 100; // PFS byte array starts at offset 100
    private const int PfsSize = 8088; // PFS byte array is 8088 bytes/pages

    public IPageService PageService { get; } = pageService;

    public async Task<PfsPage> Load(Database database, PageAddress pageAddress)
    {
        var page = await PageService.Load<PfsPage>(database, pageAddress);

        if (page.Header.PageType != PageType.Pfs)
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