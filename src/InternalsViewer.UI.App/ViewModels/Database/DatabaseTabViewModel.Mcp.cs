using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Database;

public sealed partial class DatabaseTabViewModel : IMcpContextSource, IAllocationsCommands
{
    public TabContext GetContext()
        => new AllocationsContext
        {
            TabId = TabId,
            Title = Name,
            Database = Database,
            FileId = FileId,
            Filter = Filter,
            Overlay = HasOverlay ? Overlay : null,
            ShowsSystemObjects = IsDisplaySystemObjects,
            SelectedObjects = [.. SelectedLayers.Select(l => l.Name)],
            Files = Database.Files,
            AllocationUnits = Database.AllocationUnits,
            Pfs = Database.Pfs,
            Gam = Database.Gam,
            SGam = Database.SGam,
            Dcm = Database.Dcm,
            Bcm = Database.Bcm
        };

    public Task SelectObjectAsync(string? name, bool add)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            SelectedLayers = [];

            return Task.CompletedTask;
        }

        var layer = FindLayer(name);

        var layers = add ? SelectedLayers.ToList() : [];

        if (!layers.Contains(layer))
        {
            layers.Add(layer);
        }

        SelectedLayers = new ObservableCollection<AllocationLayer>(layers);

        return Task.CompletedTask;
    }

    public Task<PageAddress> ScrollToAsync(string? name, PageAddress? page)
    {
        var target = page;

        if (target is null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("No object or page was given to scroll to.");
            }

            var layer = FindLayer(name);

            target = layer.FirstPage != PageAddress.Empty
                ? layer.FirstPage
                : layer.PageSpans.Count > 0 ? layer.PageSpans[0].Address : null;

            if (target is null)
            {
                throw new InvalidOperationException($"{layer.Name} has no pages to scroll to.");
            }
        }

        if (!Database.Files.Any(f => f.FileId == target.Value.FileId))
        {
            throw new InvalidOperationException($"File {target.Value.FileId} is not in this database.");
        }

        FileId = target.Value.FileId;

        ScrollTarget = null;
        ScrollTarget = target;

        return Task.FromResult(target.Value);
    }

    public async Task SetOverlayAsync(AllocationOverlay? overlay)
    {
        var name = overlay switch
        {
            AllocationOverlay.Gam => "GAM",
            AllocationOverlay.SGam => "SGAM",
            AllocationOverlay.Pfs => "PFS",
            AllocationOverlay.BufferPool => "Buffer Pool",
            AllocationOverlay.Dcm => "DCM",
            AllocationOverlay.Bcm => "BCM",
            _ => "Overlay"
        };

        if (overlay == AllocationOverlay.BufferPool && !IsServerConnection)
        {
            throw new InvalidOperationException("The buffer pool overlay needs a server connection.");
        }

        await SetOverlay(name);
    }

    public Task SetSearchAsync(string? text)
    {
        Filter = text ?? string.Empty;

        return Task.CompletedTask;
    }

    private AllocationLayer FindLayer(string name)
    {
        var layers = AllocationLayers.Where(l => !l.IsAllocationLayer).ToList();

        var exact = layers.Where(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

        var matches = exact.Count > 0
            ? exact
            : [.. layers.Where(l => l.Name.Contains(name, StringComparison.OrdinalIgnoreCase))];

        if (matches.Count > 1)
        {
            var main = matches.Where(l => l.IndexType is IndexType.Heap
                                                         or IndexType.Clustered
                                                         or IndexType.ClusteredColumnStore).ToList();

            if (main.Count == 1)
            {
                matches = main;
            }
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"No object matches '{name}'."),
            _ => throw new InvalidOperationException($"'{name}' matches {matches.Count} objects.")
        };
    }
}