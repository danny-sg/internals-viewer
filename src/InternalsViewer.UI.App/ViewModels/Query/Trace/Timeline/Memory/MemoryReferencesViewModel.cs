using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferencesViewModel(PlayheadMemoryViewModel owner) : ObservableObject
{
    private const string NoPointers = "No Pointers";

    private const string NoMatches = "No Matching References";

    [ObservableProperty]
    private List<MemoryReferenceNode> _roots = [];

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _filter = string.Empty;

    private PlayheadMemoryViewModel Owner { get; } = owner;

    private List<MemoryReferenceNode> AllRoots { get; set; } = [];

    public void Clear()
    {
        AllRoots = [];

        Roots = [];

        EmptyText = string.Empty;
    }

    public void SetPointers(List<MemoryHistoryPointer> pointers)
    {
        AllRoots = [.. pointers.Select(p => CreateNode(p, null))];

        ApplyFilter();
    }

    public void OpenGraph()
    {
        if (Owner.IsOpen)
        {
            Owner.Navigator?.OpenReferenceGraph(Owner.Block, Owner.Position);
        }
    }

    public async Task<List<MemoryReferenceNode>> GetChildrenAsync(MemoryReferenceNode node)
    {
        if (!Owner.IsOpen || !node.CanExpand)
        {
            return [];
        }

        var history = Owner.History;

        var block = node.Pointer.Block;

        var address = node.Pointer.Value;

        var position = Owner.Position;

        var pointers = await Task.Run(() => MemoryHistoryPointers.Read(history.Store, history.BufferPool, block, address, position));

        return Owner.IsOpen ? [.. pointers.Select(p => CreateNode(p, node))] : [];
    }

    public void Open(MemoryReferenceNode node)
    {
        if (!node.CanOpen)
        {
            return;
        }

        if (node.Pointer.Block >= 0)
        {
            Owner.RequestMemory(node.Pointer.Value, position: Owner.Position);
        }
        else if (node.Pointer.Page is { } page)
        {
            Owner.Navigator?.OpenBufferPage(page);
        }
    }

    public void Select(MemoryReferenceNode node)
    {
        if (node.Parent is null)
        {
            Owner.SelectedChangeSpan = new LogRecordAnnotation
            {
                Offset = node.Pointer.Offset,
                Length = MemoryHistoryPointers.PointerSize,
                Description = node.Target
            };
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = Filter.Trim();

        var address = GetAddressFilter(filter);

        Roots = filter.Length == 0 ? AllRoots : [.. AllRoots.Where(r => IsMatch(r, filter, address))];

        EmptyText = AllRoots.Count == 0 ? NoPointers : Roots.Count == 0 ? NoMatches : string.Empty;
    }

    private static bool IsMatch(MemoryReferenceNode node, string filter, string? address)
        => node.Target.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || (address is not null && node.Pointer.Value.ToString("X").Contains(address, StringComparison.OrdinalIgnoreCase));

    private static string? GetAddressFilter(string filter)
    {
        var digits = filter.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? filter[2..] : filter;

        digits = digits.TrimStart('0');

        return digits.Length > 0 && digits.All(Uri.IsHexDigit) ? digits : null;
    }

    private MemoryReferenceNode CreateNode(MemoryHistoryPointer pointer, MemoryReferenceNode? parent)
    {
        var history = Owner.History;

        var target = pointer switch
        {
            { Block: >= 0 } => history.GetPointerTarget(pointer.Value, Owner.Position),
            { Page: { } page } => history.GetBufferPageLabel(page),
            _ => null
        };

        var canOpen = pointer.Block >= 0 || (pointer.Page is { } reference && history.IsQueryDatabase(reference));

        return new MemoryReferenceNode(pointer, target ?? $"0x{pointer.Value:X16}", IsCycle(pointer, parent), canOpen, parent);
    }

    private bool IsCycle(MemoryHistoryPointer pointer, MemoryReferenceNode? parent)
    {
        if (pointer.Block < 0)
        {
            return false;
        }

        if (Owner.Block >= 0 && pointer.Value == Owner.History.Store.Blocks[Owner.Block].Address)
        {
            return true;
        }

        for (var node = parent; node is not null; node = node.Parent)
        {
            if (node.Pointer.Value == pointer.Value)
            {
                return true;
            }
        }

        return false;
    }
}
