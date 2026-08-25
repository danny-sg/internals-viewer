using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Chains;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.Models.Columnstore;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// The rows a columnstore index has marked deleted, which stay in their segments until the row group is rebuilt
/// </summary>
/// <remarks>
/// The bitmap is a B-tree of index records with two columns, read positionally: the row group with its low bit
/// reserved, then the row within it. The record loaders decode it as they would any other index record.
/// </remarks>
public sealed partial class DeleteBitmapTabViewModel(IPageService pageService,
                                                     IIamChainService iamChainService,
                                                     IRecordService recordService,
                                                     DatabaseSource database,
                                                     ColumnStoreIndex index) : ObservableObject
{
    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _statusText = "Loading delete bitmap";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllocationUnitDescription))]
    [NotifyPropertyChangedFor(nameof(FirstPage))]
    [NotifyPropertyChangedFor(nameof(FirstIamPage))]
    private AllocationUnit? _allocationUnit;

    [ObservableProperty]
    private IReadOnlyList<DeletedRowSummary> _deletedRows = [];

    public DatabaseSource Database { get; } = database;

    public string Title => "Delete Bitmap";

    public string HobtDescription => $"{Index.DeleteBitmap?.HobtId ?? 0}";

    public string AllocationUnitDescription => AllocationUnit is { } unit ? $"{unit.AllocationUnitId}" : string.Empty;

    public PageAddress FirstPage => AllocationUnit?.FirstPage ?? PageAddress.Empty;

    public PageAddress FirstIamPage => AllocationUnit?.FirstIamPage ?? PageAddress.Empty;
    private IPageService PageService { get; } = pageService;

    private IIamChainService IamChainService { get; } = iamChainService;

    private IRecordService RecordService { get; } = recordService;

    private ColumnStoreIndex Index { get; } = index;

    public async Task Load(CancellationToken cancellationToken)
    {
        try
        {
            if (Index.DeleteBitmap is not { IsAllocated: true } bitmap
                || bitmap.DataAllocationUnit is not { } unit)
            {
                StatusText = "The index has no allocated delete bitmap";

                return;
            }

            AllocationUnit = unit;

            var chain = await IamChainService.LoadChain(Database, unit.FirstIamPage, cancellationToken);

            var deletedRows = new List<DeletedRowSummary>();

            foreach (var address in GetPageAddresses(chain, unit.FirstIamPage.FileId))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Typed loads throw on anything else, and the ranges cover allocation pages as well as data ones
                var page = await PageService.GetPage(Database, address, cancellationToken, isMarkEnabled: false);

                // A mixed extent holds pages of other objects, so the IAM's ranges are not the rowset on their own
                if (page is not AllocationUnitPage dataPage
                    || page.PageHeader.AllocationUnitId != unit.AllocationUnitId
                    || page.PageHeader.PageType != PageType.Data
                    || page.PageHeader.Level != 0)
                {
                    continue;
                }

                foreach (var record in RecordService.GetRecords(dataPage))
                {
                    if (Read(record.Fields.Select(f => f.Value).ToList()) is { } deleted)
                    {
                        deletedRows.Add(deleted);
                    }
                }
            }

            DeletedRows = deletedRows;

            StatusText = $"{DeletedRows.Count} deleted rows";

            IsLoaded = true;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    /// <summary>
    /// Reads the two columns by position, the names the loader gives them belonging to whatever structure it found
    /// </summary>
    private static DeletedRowSummary? Read(IReadOnlyList<string> fields)
    {
        if (fields.Count < 2
            || !long.TryParse(fields[0], out var group)
            || !long.TryParse(fields[1], out var rowId))
        {
            return null;
        }

        // The row group keeps its low bit reserved, the same as a store by value segment does with its values
        return new DeletedRowSummary((int)(group >> 1), rowId);
    }

    private static IEnumerable<PageAddress> GetPageAddresses(IamChain chain, short fileId)
    {
        foreach (var page in chain.SinglePageSlots.Where(p => p != PageAddress.Empty))
        {
            yield return page;
        }

        foreach (var (from, to) in chain.GetAllocatedPageRanges(fileId))
        {
            for (var pageId = from; pageId <= to; pageId++)
            {
                yield return new PageAddress(fileId, pageId);
            }
        }
    }
}
