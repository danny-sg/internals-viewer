using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Segment;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed partial class DeltaStoreTabViewModel(IPageService pageService,
                                                   IIamChainService iamChainService,
                                                   DatabaseSource database,
                                                   RowGroupSummary rowGroup) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllocationUnitDescription))]
    [NotifyPropertyChangedFor(nameof(FirstPage))]
    [NotifyPropertyChangedFor(nameof(FirstIamPage))]
    private AllocationUnit? _allocationUnit;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "Loading Delta Store...";

    [ObservableProperty]
    private IReadOnlyList<DeltaStorePageSummary> _pages = [];

    public DatabaseSource Database { get; } = database;

    public RowGroupSummary RowGroup { get; } = rowGroup;

    public string Title => $"Delta Store, Row Group {RowGroup.RowGroupId}";

    public string StateDescription => RowGroup.State.ToString();

    public string HobtDescription => $"{RowGroup.DeltaStoreHobtId}";

    public string AllocationUnitDescription => AllocationUnit is { } unit ? $"{unit.AllocationUnitId}" : string.Empty;

    public PageAddress FirstPage => AllocationUnit?.FirstPage ?? PageAddress.Empty;

    public PageAddress FirstIamPage => AllocationUnit?.FirstIamPage ?? PageAddress.Empty;

    private IPageService PageService { get; } = pageService;

    private IIamChainService IamChainService { get; } = iamChainService;

    public async Task Load(CancellationToken cancellationToken)
    {
        try
        {
            var allocationUnit = Database.AllocationUnits
                                         .Values
                                         .FirstOrDefault(a => a.PartitionId == RowGroup.DeltaStoreHobtId
                                                              && a.AllocationUnitType == AllocationUnitType.InRowData);

            if (allocationUnit is null)
            {
                StatusText = $"No allocation unit for HoBT {RowGroup.DeltaStoreHobtId}";

                return;
            }

            AllocationUnit = allocationUnit;

            var chain = await IamChainService.LoadChain(Database, allocationUnit.FirstIamPage, cancellationToken);

            var pages = new List<DeltaStorePageSummary>();

            foreach (var address in GetPageAddresses(chain))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!IsAllocated(address))
                {
                    continue;
                }

                var page = await PageService.GetPage(Database, address, cancellationToken, isMarkEnabled: false);

                if (page.PageHeader.AllocationUnitId != allocationUnit.AllocationUnitId
                    || page.PageHeader.PageType is not (PageType.Data or PageType.Index))
                {
                    continue;
                }

                pages.Add(new DeltaStorePageSummary(address,
                                                    page.PageHeader.SlotCount,
                                                    page.PageHeader.FreeCount,
                                                    page.PageHeader.PageType.ToString()));
            }

            Pages = pages;

            StatusText = $"{Pages.Count} pages";

            IsLoaded = true;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private bool IsAllocated(PageAddress address)
    {
        if (!Database.Pfs.TryGetValue(address.FileId, out var pfs))
        {
            return true;
        }

        return pfs.GetPageStatus(address.PageId).IsAllocated;
    }

    private static IEnumerable<PageAddress> GetPageAddresses(IamChain chain)
    {
        foreach (var page in chain.SinglePageSlots.Where(p => p != PageAddress.Empty))
        {
            yield return page;
        }

        foreach (var iamPage in chain.Pages)
        {
            var fileId = iamPage.PageAddress.FileId;

            foreach (var (from, to) in chain.GetAllocatedPageRanges(fileId))
            {
                for (var pageId = from; pageId <= to; pageId++)
                {
                    yield return new PageAddress(fileId, pageId);
                }
            }

            break;
        }
    }
}
