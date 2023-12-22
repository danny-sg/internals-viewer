using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Loaders.Chains;

/// <summary>
/// Service responsible for building PFS chains
/// </summary>
public class PfsChainService(IPageService pageService) : IPfsChainService
{
    public IPageService PageService { get; } = pageService;

    /// <summary>
    /// Load all PFS pages for a file
    /// </summary>
    public async Task<PfsChain> LoadChain(DatabaseDetail databaseDetail, short fileId)
    {
        // Calculate the number of PFS pages in the file
        var pfsCount = (int)Math.Ceiling(databaseDetail.GetFileSize(fileId) / (decimal)PfsPage.PfsInterval);

        var pfsChain = new PfsChain();

        // The first PFS page is always page 1
        var page = await PageService.GetPage<PfsPage>(databaseDetail, new PageAddress(fileId, 1));

        pfsChain.PfsPages.Add(page);

        if (pfsCount > 1)
        {
            for (var i = 1; i < pfsCount; i++)
            {
                // Further PFS pages are loaded on an interval basis
                var nextAddress = new PageAddress(fileId, i * PfsPage.PfsInterval);

                var nextPage = await PageService.GetPage<PfsPage>(databaseDetail, nextAddress);

                pfsChain.PfsPages.Add(nextPage);
            }
        }

        return pfsChain;
    }
}