using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryHashTableNode(MemoryHashTableNodeType type,
                                        string name,
                                        ulong address,
                                        int partition,
                                        MemoryHashTableNode? parent)
{
    public MemoryHashTableNodeType Type { get; } = type;

    public string Name { get; } = name;

    public ulong Address { get; } = address;

    public int Partition { get; } = partition;

    public MemoryHashTableNode? Parent { get; } = parent;

    public ulong Target { get; init; }

    public int FirstBucket { get; init; }

    public string Detail { get; init; } = string.Empty;

    public List<MemoryHashTableBadge> Badges { get; init; } = [];

    public List<MemoryHashTableNode> Children { get; } = [];

    public string Key => $"{Type}:{Partition}:{Address:X}:{Target:X}";

    public string AddressText => Address == 0 ? string.Empty : $"0x{Address:X16}";

    public string TargetText => Target == 0 ? string.Empty : $"0x{Target:X16}";

    public bool HasTarget => Target != 0;

    public bool IsSelectable => Type is MemoryHashTableNodeType.Group
                                     or MemoryHashTableNodeType.Partition
                                     or MemoryHashTableNodeType.BucketPage
                                     or MemoryHashTableNodeType.OverflowPage
                                     or MemoryHashTableNodeType.WorkfilePage;
}
