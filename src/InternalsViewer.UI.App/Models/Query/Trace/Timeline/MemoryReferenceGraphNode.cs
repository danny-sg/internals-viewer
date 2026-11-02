using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed partial class MemoryReferenceGraphNode(string types,
                                                     double x,
                                                     double y,
                                                     double height,
                                                     int watchCount,
                                                     bool isRoot,
                                                     bool isData,
                                                     bool isExpanded,
                                                     bool isReferrersExpanded,
                                                     MemoryReferenceGraphSpan[] activeSpans,
                                                     MemoryReferenceGraphMark[] marks,
                                                     string dataChanges,
                                                     Action? open,
                                                     Action? toggle,
                                                     Action? toggleReferrers,
                                                     Action? select,
                                                     Action? expandAll,
                                                     Action? collapseAll,
                                                     Action<bool>? hover) : ObservableObject
{
    [ObservableProperty]
    private string _label = types;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private bool _canOpen;

    [ObservableProperty]
    private MemoryReferenceGraphWatch[] _watches = [];

    public string Types { get; } = types;

    public double X { get; } = x;

    public double Y { get; } = y;

    public double Height { get; } = height;

    public int WatchCount { get; } = watchCount;

    public bool IsRoot { get; } = isRoot;

    public bool IsData { get; } = isData;

    public bool IsExpanded { get; } = isExpanded;

    public bool IsReferrersExpanded { get; } = isReferrersExpanded;

    public MemoryReferenceGraphSpan[] ActiveSpans { get; } = activeSpans;

    public MemoryReferenceGraphMark[] Marks { get; } = marks;

    public string DataChanges { get; } = dataChanges;

    public Action? Open { get; } = open;

    public Action? Toggle { get; } = toggle;

    public Action? ToggleReferrers { get; } = toggleReferrers;

    public Action? Select { get; } = select;

    public Action? ExpandAll { get; } = expandAll;

    public Action? CollapseAll { get; } = collapseAll;

    public Action<bool>? Hover { get; } = hover;
}
