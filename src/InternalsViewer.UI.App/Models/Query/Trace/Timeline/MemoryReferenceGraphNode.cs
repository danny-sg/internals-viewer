using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed partial class MemoryReferenceGraphNode(string types,
                                                     double x,
                                                     double y,
                                                     bool isRoot,
                                                     bool isData,
                                                     bool isExpanded,
                                                     bool isReferrersExpanded,
                                                     (double Start, double End)[] activeSpans,
                                                     MemoryReferenceGraphMark[] marks,
                                                     string dataChanges,
                                                     Action? open,
                                                     Action? toggle,
                                                     Action? toggleReferrers) : ObservableObject
{
    [ObservableProperty]
    private string _label = types;

    [ObservableProperty]
    private string _detail = string.Empty;

    [ObservableProperty]
    private bool _canOpen;

    public string Types { get; } = types;

    public double X { get; } = x;

    public double Y { get; } = y;

    public bool IsRoot { get; } = isRoot;

    public bool IsData { get; } = isData;

    public bool IsExpanded { get; } = isExpanded;

    public bool IsReferrersExpanded { get; } = isReferrersExpanded;

    public (double Start, double End)[] ActiveSpans { get; } = activeSpans;

    public MemoryReferenceGraphMark[] Marks { get; } = marks;

    public string DataChanges { get; } = dataChanges;

    public Action? Open { get; } = open;

    public Action? Toggle { get; } = toggle;

    public Action? ToggleReferrers { get; } = toggleReferrers;
}
