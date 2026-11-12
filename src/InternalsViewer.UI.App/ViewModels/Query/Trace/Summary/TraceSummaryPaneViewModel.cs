using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public abstract partial class TraceSummaryPaneViewModel(string title,
                                                        MemoryHistoryViewModel history,
                                                        Func<ITraceNavigator> getNavigator) : ObservableObject
{
    [ObservableProperty]
    private List<TraceSummaryNode> _roots = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public string Title { get; } = title;

    public virtual bool IsFiltered => false;

    public virtual bool IsExpandingAll => false;

    protected MemoryHistoryViewModel History { get; } = history;

    private Func<ITraceNavigator> GetNavigator { get; } = getNavigator;

    private bool IsShown { get; set; }

    private bool IsBuilt { get; set; }

    public void Show()
    {
        IsShown = true;

        if (!IsBuilt)
        {
            IsBuilt = true;

            Build();
        }
    }

    public void Reset()
    {
        Clear();

        IsBuilt = false;

        Roots = [];

        EmptyText = string.Empty;

        if (IsShown)
        {
            IsBuilt = true;

            Build();
        }
    }

    public void Toggle() => IsExpanded = !IsExpanded;

    public virtual void SetPlayhead(TracePosition position)
    {
    }

    public virtual List<TraceSummaryNode> GetChildren(TraceSummaryNode node) => node.Children ?? [];

    public void Open(TraceSummaryNode node)
    {
        if (!node.IsObject || node.Block >= History.Store.Blocks.Count)
        {
            return;
        }

        var position = node.Position ?? History.Store.Blocks[node.Block].FromPosition;

        if (node.LineageColumn is { } column)
        {
            GetNavigator().OpenIteratorTab(node.Block, node.Address, position, [MemoryIteratorViewModel.LineageHeader, column]);

            return;
        }

        GetNavigator().OpenMemory(node.Block, node.Address, position);
    }

    public void OpenHashTable(TraceSummaryNode node)
    {
        if (node.HashTableGroup != 0)
        {
            GetNavigator().OpenHashTable(node.HashTableGroup, node.HashTablePart);
        }
    }

    public void OpenGraph(TraceSummaryNode node)
    {
        if (node.IsObject && node.Block < History.Store.Blocks.Count)
        {
            GetNavigator().OpenReferenceGraph(node.Block, History.Store.Blocks[node.Block].FromPosition);
        }
    }

    protected static string GetAddressText(ulong address) => $"0x{address:X16}";

    protected static string GetCountText(int count, string noun)
        => count == 1 ? $"1 {noun}" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} {noun}s";

    protected abstract void Build();

    protected abstract void Clear();
}
