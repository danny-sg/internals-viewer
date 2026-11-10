using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.Views.Query.Page;
using Microsoft.Extensions.Logging;
using LogRecordItem = InternalsViewer.UI.App.Models.Logging.LogRecordItem;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    private Dictionary<PlanNode, FollowedPage> FollowedPages { get; } = new(ReferenceEqualityComparer.Instance);

    private int FollowedPageCount { get; set; }

    public void OpenOperatorPage(PlanPage page) => _ = OpenOperatorPageAsync(page);

    private Task OpenOperatorPageAsync(PlanPage page)
    {
        var logRecords = GetPageLogRecords(page.Page);

        if (FollowedPages.TryGetValue(page.Node, out var followed) && Layout.TryGetDocument(followed.Key, out var existing))
        {
            Layout.Show(existing);

            return LoadPageDocument(followed.ViewModel, page.Page, page.Slot, null, logRecords);
        }

        FollowedPageCount++;

        var key = $"Page:Operator:{FollowedPageCount}";

        var viewModel = CreatePageDocument(key, $"{page.Node.PhysicalOperator} Page");

        FollowedPages[page.Node] = new FollowedPage(key, viewModel);

        return LoadPageDocument(viewModel, page.Page, page.Slot, null, logRecords);
    }

    private PageTabViewModel CreatePageDocument(string key, string title)
    {
        var viewModel = _pageTabViewModelFactory.Create(Database);

        var document = new DocumentViewModel(title: title,
                                             content: viewModel,
                                             viewFactory: static () => new QueryPageTabView(),
                                             canClose: true,
                                             keepAlive: true,
                                             key: key,
                                             persist: false);

        Layout.RegisterDocument(key, document);

        _openPages[key] = viewModel;

        Layout.Show(document);

        return viewModel;
    }

    private void SyncOpenPages(IReadOnlyList<PlanPage> pages)
    {
        foreach (var page in pages)
        {
            if (FollowedPages.TryGetValue(page.Node, out var followed))
            {
                _ = FollowPageAsync(followed.ViewModel, page);
            }

            if (page.Slot is not null && _openPages.TryGetValue($"Page:{page.Page}", out var open) && open.PageAddress == page.Page)
            {
                _ = FollowPageAsync(open, page);
            }
        }
    }

    private async Task FollowPageAsync(PageTabViewModel viewModel, PlanPage page)
    {
        try
        {
            var isNewPage = viewModel.PageAddress != page.Page;

            await viewModel.MoveTo(page.Page, page.Slot);

            if (isNewPage)
            {
                viewModel.LogRecords = new ObservableCollection<LogRecordItem>(
                    GetPageLogRecords(page.Page).Select(r => new LogRecordItem { Record = r }));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error moving to page {PageAddress}", page.Page);
        }
    }

    private void RemoveClosedFollowedPages()
    {
        foreach (var node in FollowedPages.Where(f => !_openPages.ContainsKey(f.Value.Key)).Select(f => f.Key).ToList())
        {
            FollowedPages.Remove(node);
        }
    }

    private sealed record FollowedPage(string Key, PageTabViewModel ViewModel);
}
