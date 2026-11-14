using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed partial class MemoryStructureNode(MemoryStructureNodeType type,
                                                string name,
                                                ulong address,
                                                int partition,
                                                MemoryStructureNode? parent) : ObservableObject
{
    private const double InactiveOpacity = 0.45;

    [ObservableProperty]
    private string _value = string.Empty;

    public MemoryStructureNodeType Type { get; } = type;

    public string Name { get; } = name;

    public ulong Address { get; } = address;

    public int Partition { get; } = partition;

    public MemoryStructureNode? Parent { get; } = parent;

    public bool IsActive { get; init; } = true;

    public double Opacity => IsActive ? 1 : InactiveOpacity;

    public int FirstBucket { get; init; }

    public int Index { get; init; } = -1;

    public string Detail { get; init; } = string.Empty;

    public bool IsAddressVisible { get; init; } = true;

    public SqlTypeInfo? ColumnType { get; init; }

    public bool HasColumnType => ColumnType is not null;

    public List<MemoryStructureBadge> Badges { get; init; } = [];

    public List<MemoryStructureNode> Children { get; } = [];

    public ulong Group => Type is MemoryStructureNodeType.Group or MemoryStructureNodeType.WorktableGroup ? Address : Parent?.Group ?? 0;

    public string Key => $"{Type}:{Group:X}:{Partition}:{Address:X}:{Index}";

    public string AddressText => Address == 0 || !IsAddressVisible ? string.Empty : $"0x{Address:X16}";

    public bool IsSelectable => Type is not (MemoryStructureNodeType.BuildSide
                                             or MemoryStructureNodeType.ProbeSide
                                             or MemoryStructureNodeType.BucketPages
                                             or MemoryStructureNodeType.WorkfilePages
                                             or MemoryStructureNodeType.Runs
                                             or MemoryStructureNodeType.RecordPages
                                             or MemoryStructureNodeType.ComparePages);

    public bool HasData => IsSelectable
                           && Type is not (MemoryStructureNodeType.Run
                                           or MemoryStructureNodeType.Memory
                                           or MemoryStructureNodeType.Statement
                                           or MemoryStructureNodeType.Expression);

    public bool IsPage => Type is MemoryStructureNodeType.WorkfilePage or MemoryStructureNodeType.RecordPage;

    public bool IsCalculator => Type == MemoryStructureNodeType.Expression;
}
