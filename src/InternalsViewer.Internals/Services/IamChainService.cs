using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders;

namespace InternalsViewer.Internals.Services;

/// <summary>
/// Service responsible for building IAM chains
/// </summary>
public class IamChainService(IAllocationPageLoader pageLoader) : IIamChainService
{
    public IAllocationPageLoader PageLoader { get; } = pageLoader;

    /// <summary>
    /// Loads an IAM chain
    /// </summary>
    /// <remarks>
    /// IAM chains are linked lists of pages linked via the Next Page/Previous Page pointers in the page header
    /// </remarks>
    public async Task<IamChain> LoadChain(DatabaseDetail databaseDetail, PageAddress startPageAddress)
    {
        var iam = new IamChain();

        var pageAddress = startPageAddress; 

        while (true)
        {
            var page = await PageLoader.Load(databaseDetail, pageAddress);

            iam.Pages.Add(page);

            iam.SinglePageSlots.AddRange(page.SinglePageSlots);

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