using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Loaders.Chains;

/// <summary>
/// Service responsible for building IAM chains
/// </summary>
public class IamChainService(IPageService pageService) : IIamChainService
{
    public IPageService PageService { get; } = pageService;
    
    /// <summary>
    /// Loads an IAM chain
    /// </summary>
    /// <remarks>
    /// IAM chains are linked lists of pages linked via the Next Page/Previous Page pointers in the page header
    /// </remarks>
    public async Task<IamChain> LoadChain(DatabaseSource database, PageAddress startPageAddress)
    {
        var iam = new IamChain();

        var pageAddress = startPageAddress;

        while (true)
        {
            var page = await PageService.GetPage<IamPage>(database, pageAddress);

            iam.Pages.Add(page);

            iam.SinglePageSlots = iam.SinglePageSlots.Concat(page.SinglePageSlots).ToArray();

            if (page.PageHeader.NextPage != PageAddress.Empty)
            {
                pageAddress = page.PageHeader.NextPage;

                continue;
            }

            break;
        }

        return iam;
    }
}