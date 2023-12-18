using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services;

/// <summary>
/// Service responsible for building PFS chains
/// </summary>
public class PfsChainService(IPfsPageService pageService) : IPfsChainService
{
    public IPfsPageService PageService { get; set; } = pageService;

    public async Task<PfsChain> LoadChain(DatabaseDetail databaseDetail, short fileId)
    {
        var pfsCount = (int)Math.Ceiling(databaseDetail.GetFileSize(fileId) / (decimal)PfsPage.PfsInterval);

        var pfsChain = new PfsChain();

        var page = await PageService.Load(databaseDetail, new PageAddress(fileId, 1));

        pfsChain.PfsPages.Add(page);

        if (pfsCount > 1)
        {
            for (var i = 1; i < pfsCount; i++)
            {
                var nextAddress = new PageAddress(fileId, i * PfsPage.PfsInterval);

                var nextPage = await PageService.Load(databaseDetail, nextAddress);

                pfsChain.PfsPages.Add(nextPage);
            }
        }

        return pfsChain;
    }
}