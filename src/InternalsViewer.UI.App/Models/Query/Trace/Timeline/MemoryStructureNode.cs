using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryStructureNode(MemoryStructureNodeType type,
                                        string name,
                                        ulong address,
                                        int partition,
                                        MemoryStructureNode? parent)
{
    private const double InactiveOpacity = 0.45;

    public MemoryStructureNodeType Type { get; } = type;

    public string Name { get; } = name;

    public ulong Address { get; } = address;

    public int Partition { get; } = partition;

    public MemoryStructureNode? Parent { get; } = parent;

    public bool IsActive { get; init; } = true;

    public double Opacity => IsActive ? 1 : InactiveOpacity;

    public int FirstBucket { get; init; }

    public string Detail { get; init; } = string.Empty;

    public List<MemoryStructureBadge> Badges { get; init; } = [];

    public List<MemoryStructureNode> Children { get; } = [];

    public ulong Group => Type is MemoryStructureNodeType.Group or MemoryStructureNodeType.WorktableGroup ? Address : Parent?.Group ?? 0;

    public string Key => $"{Type}:{Group:X}:{Partition}:{Address:X}";

    public string AddressText => Address == 0 ? string.Empty : $"0x{Address:X16}";

    public bool IsSelectable => Type is not (MemoryStructureNodeType.BuildSide
                                             or MemoryStructureNodeType.ProbeSide
                                             or MemoryStructureNodeType.BucketPages
                                             or MemoryStructureNodeType.WorkfilePages
                                             or MemoryStructureNodeType.Runs
                                             or MemoryStructureNodeType.RecordPages
                                             or MemoryStructureNodeType.ComparePages);

    public bool HasData => IsSelectable && Type is not (MemoryStructureNodeType.Run or MemoryStructureNodeType.Memory);

    public bool IsPage => Type is MemoryStructureNodeType.WorkfilePage or MemoryStructureNodeType.RecordPage;
}
