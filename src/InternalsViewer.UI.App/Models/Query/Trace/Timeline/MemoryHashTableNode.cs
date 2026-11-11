using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryHashTableNode(MemoryHashTableNodeType type,
                                        string name,
                                        ulong address,
                                        int partition,
                                        MemoryHashTableNode? parent)
{
    private const double InactiveOpacity = 0.45;

    public MemoryHashTableNodeType Type { get; } = type;

    public string Name { get; } = name;

    public ulong Address { get; } = address;

    public int Partition { get; } = partition;

    public MemoryHashTableNode? Parent { get; } = parent;

    public bool IsActive { get; init; } = true;

    public double Opacity => IsActive ? 1 : InactiveOpacity;

    public int FirstBucket { get; init; }

    public string Detail { get; init; } = string.Empty;

    public List<MemoryHashTableBadge> Badges { get; init; } = [];

    public List<MemoryHashTableNode> Children { get; } = [];

    public ulong Group => Type is MemoryHashTableNodeType.Group or MemoryHashTableNodeType.WorktableGroup ? Address : Parent?.Group ?? 0;

    public string Key => $"{Type}:{Group:X}:{Partition}:{Address:X}";

    public string AddressText => Address == 0 ? string.Empty : $"0x{Address:X16}";

    public bool IsSelectable => Type is MemoryHashTableNodeType.HashTable
                                     or MemoryHashTableNodeType.Group
                                     or MemoryHashTableNodeType.WorktableGroup
                                     or MemoryHashTableNodeType.Accessors
                                     or MemoryHashTableNodeType.Partition
                                     or MemoryHashTableNodeType.BucketPage
                                     or MemoryHashTableNodeType.OverflowPage
                                     or MemoryHashTableNodeType.WorkfilePage;
}
