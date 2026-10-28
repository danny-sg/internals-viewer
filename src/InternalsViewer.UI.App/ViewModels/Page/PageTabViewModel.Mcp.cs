using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Page;

public sealed partial class PageTabViewModel : IMcpContextSource, IPageCommands
{
    public TabContext GetContext()
    {
        var marker = SelectedMarker;

        var selection = marker is not null && Page.Data.Length > marker.EndPosition
            ? Page.Data[marker.StartPosition..(marker.EndPosition + 1)]
            : null;

        var availableTabs = GetAvailableTabs();

        return new PageContext
        {
            TabId = TabId,
            Title = Name,
            Database = Database,
            AllocationUnit = AllocationUnit,
            Page = Page is EmptyPage ? null : Page,
            Address = PageAddress,
            CurrentSlot = SelectedSlot?.Index,
            Structures = [.. PageSlots.Where(s => s.Index < 0).Select(s => new PageStructure(s.Index, s.Description))],
            SelectedName = marker?.Name,
            SelectedValue = marker?.Value,
            SelectionStart = marker?.StartPosition ?? 0,
            Selection = selection,
            Records = [.. Records],
            View = MarkerTabName,
            Tab = availableTabs.Contains((PageViewTab)SelectedTabIndex) ? (PageViewTab)SelectedTabIndex : PageViewTab.Markers,
            AvailableTabs = availableTabs,
            MarkerNames = [.. Flatten(Markers).Select(m => m.Name).Where(n => n.Length > 0)]
        };
    }

    public async Task GoToPageAsync(PageAddress address, int? slot)
    {
        await LoadPage(address, slot is { } value ? (ushort)value : null);

        if (Page is EmptyPage || PageAddress != address)
        {
            throw new InvalidOperationException($"Page {address} could not be loaded.");
        }
    }

    public Task SelectSlotAsync(int slot)
    {
        SelectedSlot = PageSlots.FirstOrDefault(s => s.Index == slot)
                       ?? throw new InvalidOperationException($"Page {PageAddress} has no slot {slot}.");

        return Task.CompletedTask;
    }

    public Task SelectMarkerAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SelectedMarker = null;

            return Task.CompletedTask;
        }

        var markers = Flatten(Markers).ToList();

        var marker = markers.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                     ?? markers.FirstOrDefault(m => m.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"No marker matches '{name}'.");

        SelectedMarker = marker;

        ScrollToOffset = null;
        ScrollToOffset = marker.StartPosition;

        return Task.CompletedTask;
    }

    public Task ScrollToAsync(int offset)
    {
        if (offset < 0 || offset >= PageData.Size)
        {
            throw new InvalidOperationException($"Offset {offset} is outside the page.");
        }

        ScrollToOffset = null;
        ScrollToOffset = offset;

        return Task.CompletedTask;
    }

    public Task ShowTabAsync(PageViewTab tab)
    {
        if (!GetAvailableTabs().Contains(tab))
        {
            throw new InvalidOperationException($"This page has no {tab} view.");
        }

        SelectedTabIndex = (int)tab;

        return Task.CompletedTask;
    }

    public async Task OpenIndexAsync()
    {
        if (!OpenIndexViewCommand.CanExecute(null))
        {
            throw new InvalidOperationException($"Page {PageAddress} does not belong to an index.");
        }

        await OpenIndexViewCommand.ExecuteAsync(null);
    }

    private static IEnumerable<Marker> Flatten(IEnumerable<Marker> markers)
    {
        foreach (var marker in markers)
        {
            yield return marker;

            foreach (var child in Flatten(marker.Children))
            {
                yield return child;
            }
        }
    }

    private List<PageViewTab> GetAvailableTabs()
    {
        var tabs = new List<PageViewTab>();

        if (IsRowDataTabVisible)
        {
            tabs.Add(PageViewTab.Markers);
        }

        if (IsAllocationsTabVisible)
        {
            tabs.Add(PageViewTab.Allocations);
        }

        if (IsPfsTabVisible)
        {
            tabs.Add(PageViewTab.Pfs);
        }

        if (IsDataTabVisible)
        {
            tabs.Add(PageViewTab.Data);
        }

        return tabs;
    }
}