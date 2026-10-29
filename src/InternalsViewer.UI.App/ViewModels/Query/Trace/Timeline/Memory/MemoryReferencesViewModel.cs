using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferencesViewModel(PlayheadMemoryViewModel owner) : ObservableObject
{
    private const string NoPointers = "No Pointers";

    [ObservableProperty]
    private List<MemoryReferenceNode> _roots = [];

    [ObservableProperty]
    private string _emptyText = string.Empty;

    private PlayheadMemoryViewModel Owner { get; } = owner;

    public void Clear()
    {
        Roots = [];

        EmptyText = string.Empty;
    }

    public void SetPointers(List<MemoryPointer> pointers)
    {
        Roots = [.. pointers.Select(p => CreateNode(p, null))];

        EmptyText = Roots.Count == 0 ? NoPointers : string.Empty;
    }

    public async Task<List<MemoryReferenceNode>> GetChildrenAsync(MemoryReferenceNode node)
    {
        if (!Owner.IsOpen || !node.CanExpand)
        {
            return [];
        }

        var history = Owner.History;

        var block = node.Pointer.Block;

        var position = Owner.Position;

        var pointers = await Task.Run(() => MemoryPointers.Read(history, block, position));

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
                Length = MemoryPointers.PointerSize,
                Description = node.Target
            };
        }
    }

    private MemoryReferenceNode CreateNode(MemoryPointer pointer, MemoryReferenceNode? parent)
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

    private bool IsCycle(MemoryPointer pointer, MemoryReferenceNode? parent)
    {
        if (pointer.Block < 0)
        {
            return false;
        }

        if (pointer.Block == Owner.Block)
        {
            return true;
        }

        for (var node = parent; node is not null; node = node.Parent)
        {
            if (node.Pointer.Block == pointer.Block)
            {
                return true;
            }
        }

        return false;
    }
}
