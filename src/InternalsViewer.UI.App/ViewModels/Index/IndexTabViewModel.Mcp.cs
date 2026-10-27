using System;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Indexes;

namespace InternalsViewer.UI.App.ViewModels.Index;

public partial class IndexTabViewModel : IMcpContextSource, IIndexCommands
{
    private const int ZoomAnimationMs = 600;

    private const float DefaultZoom = 2f;

    private const int RecordsLoadTimeoutMs = 5000;

    private const int RecordsPollMs = 20;

    public TabContext GetContext()
        => new IndexContext
        {
            TabId = TabId,
            Title = Name,
            Database = Database,
            AllocationUnit = AllocationUnit,
            Nodes = Nodes,
            RootPage = RootPage,
            SelectedNode = Nodes.Find(n => n.PageAddress == SelectedPageAddress),
            SelectedSlot = SelectedSlot,
            Records =
            [
                .. Records.Select(r => new IndexRecordContext(
                    r.Slot,
                    r.DownPagePointer == PageAddress.Empty ? null : r.DownPagePointer.ToString(),
                    [.. r.Fields.Select(f => $"{f.Name}={f.Value}")]))
            ]
        };

    public async Task<string> ZoomToPageAsync(PageAddress address, float? zoom)
    {
        RequireNode(address);

        if (zoom is <= 0)
        {
            throw new InvalidOperationException("Zoom must be greater than zero.");
        }

        var previous = Zoom;

        await LoadPage(address);

        await WaitForRecordsAsync(address);

        SelectedSlot = null;

        ZoomToPageTarget = zoom ?? DefaultZoom;

        _ = ClearZoomTargetAsync();

        return $"Selected page {address} and zoomed from {previous:0.##} to {ZoomToPageTarget:0.##}.";
    }

    public async Task<string> SelectPageAsync(PageAddress? address)
    {
        if (address is null)
        {
            await LoadPage(PageAddress.Empty);

            SelectedPageAddress = null;
            SelectedSlot = null;

            return "Cleared the page selection.";
        }

        RequireNode(address.Value);

        SelectedSlot = null;

        await LoadPage(address.Value);

        await WaitForRecordsAsync(address.Value);

        return $"Selected page {address} with {Records.Count} record(s) in the details pane.";
    }

    public Task<string> SelectRowAsync(int? slot)
    {
        if (slot is null)
        {
            SelectedSlot = null;

            return Task.FromResult("Cleared the row selection.");
        }

        if (SelectedPageAddress is null || Records.Count == 0)
        {
            throw new InvalidOperationException("No page is selected. Use index_select_page first.");
        }

        if (Records.All(r => r.Slot != slot))
        {
            throw new InvalidOperationException(
                $"Page {SelectedPageAddress} has no record in slot {slot}. It has {Records.Count} record(s).");
        }

        SelectedSlot = slot;

        return Task.FromResult($"Selected the record in slot {slot} of page {SelectedPageAddress}.");
    }

    private async Task WaitForRecordsAsync(PageAddress address)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(RecordsLoadTimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (SelectedPageAddress == address && LoadingPageAddress is null && PageLoadCancellation is null)
            {
                return;
            }

            await Task.Delay(RecordsPollMs);
        }

        throw new InvalidOperationException($"Timed out waiting for the records of page {address} to load.");
    }

    private void RequireNode(PageAddress address)
    {
        if (Nodes.All(n => n.PageAddress != address))
        {
            throw new InvalidOperationException($"Page {address} is not in this index.");
        }
    }

    private async Task ClearZoomTargetAsync()
    {
        await Task.Delay(ZoomAnimationMs);

        DispatcherQueue.TryEnqueue(() => ZoomToPageTarget = null);
    }
}