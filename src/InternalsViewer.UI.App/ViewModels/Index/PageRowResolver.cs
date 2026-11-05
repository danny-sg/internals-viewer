using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Index;

public sealed partial class PageRowResolver(IPageService pageService, DatabaseSource database, ILogger logger) : ObservableObject
{
    [ObservableProperty]
    private RowIdentifier? _row;

    [ObservableProperty]
    private int _slotCount;

    private IPageService PageService { get; } = pageService;

    private DatabaseSource Database { get; } = database;

    private ILogger Logger { get; } = logger;

    private PageData? Page { get; set; }

    private (PageAddress Page, int Offset)? Request { get; set; }

    public void Remember(PageData? page) => Page = page ?? Page;

    public void MoveTo(PageAddress? pageAddress, int? offset, CancellationToken cancellationToken)
    {
        if (pageAddress is { } page && offset is { } rowOffset)
        {
            _ = ResolveAsync(page, rowOffset, cancellationToken);

            return;
        }

        Request = null;

        if (Row?.PageAddress != pageAddress)
        {
            Row = null;
        }
    }

    public void Clear()
    {
        Request = null;

        Row = null;

        SlotCount = 0;
    }

    private async Task ResolveAsync(PageAddress pageAddress, int offset, CancellationToken cancellationToken)
    {
        Request = (pageAddress, offset);

        try
        {
            var page = Page?.PageAddress == pageAddress
                ? Page
                : await PageService.GetPage(Database, pageAddress, cancellationToken, false);

            if (Request != (pageAddress, offset))
            {
                return;
            }

            Page = page;

            SlotCount = page.PageHeader.SlotCount;

            Row = page.GetSlot(offset) is { } slot
                ? new RowIdentifier(pageAddress, (ushort)slot)
                : Row?.PageAddress == pageAddress ? Row : null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to find the slot at offset {Offset} on page {PageAddress}", offset, pageAddress);
        }
    }
}
