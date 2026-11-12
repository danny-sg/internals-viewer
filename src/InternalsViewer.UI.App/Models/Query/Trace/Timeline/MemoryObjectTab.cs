using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models.Query.Trace.Lineage;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed partial class MemoryObjectTab(string header,
                                            List<MemoryObjectTab> tabs,
                                            Action<ResultRow<long>?>? select = null,
                                            LineageGraph? graph = null)
    : ObservableObject
{
    [ObservableProperty]
    private QueryResultSet? _table;

    [ObservableProperty]
    private bool _isSelected;

    public string Header { get; } = header;

    public List<MemoryObjectTab> Tabs { get; } = tabs;

    public LineageGraph? Graph { get; } = graph;

    public bool HasTabs => Tabs.Count > 0;

    public bool HasGraph => Graph is not null;

    public bool HasTable => Tabs.Count == 0 && Graph is null;

    public ResultRow<long>? SelectedRow
    {
        get;
        set
        {
            field = value;

            select?.Invoke(value);
        }
    }

    internal string ValueKey { get; set; } = string.Empty;
}
