using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services;

/// <summary>
/// Service responsible for building IAM chains
/// </summary>
public class IamChainService(IAllocationPageService pageService) : IIamChainService
{
    public IAllocationPageService PageService { get; } = pageService;

    /// <summary>
    /// Loads an IAM chain
    /// </summary>
    /// <remarks>
    /// IAM chains are linked lists of pages linked via the Next Page/Previous Page pointers in the page header
    /// </remarks>
    public async Task<IamChain> LoadChain(Database database, PageAddress startPageAddress)
    {
        var iam = new IamChain();

        var pageAddress = startPageAddress; 

        while (true)
        {
            var page = await PageService.Load(database, pageAddress);

            iam.Pages.Add(page);

            iam.SinglePageSlots.AddRange(page.SinglePageSlots);

            if (page.Header.NextPage != PageAddress.Empty)
            {
                pageAddress = page.Header.NextPage;

                continue;
            }

            break;
        }

        return iam;
    }
}