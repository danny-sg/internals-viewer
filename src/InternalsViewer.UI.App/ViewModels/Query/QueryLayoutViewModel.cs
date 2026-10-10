using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs;
using InternalsViewer.UI.App.Views.Query.Tabs.CallStack;
using InternalsViewer.UI.App.Views.Query.Tabs.FlameChart;
using InternalsViewer.UI.App.Views.Query.Tabs.Timeline;
using Microsoft.UI.Xaml.Controls;
using QueryPlanTabCommands = InternalsViewer.UI.App.Views.Query.Tabs.Plan.QueryPlanTabCommands;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// Query View Dock management
/// </summary>
public sealed partial class QueryLayoutViewModel : ObservableObject, IDisposable
{
    private const string SqlKey = "Sql";
    private const string AllocationsKey = "Allocations";
    private const string PlanKey = "Plan";
    private const string EventsKey = "Events";
    private const string CallstackKey = "Callstack";
    private const string InstructionsKey = "Instructions";
    private const string TimelineKey = "Timeline";
    private const string FlameChartKey = "FlameChart";

    private readonly Dictionary<string, DocumentViewModel> _documentsByKey;

    private readonly HashSet<DocumentViewModel> _hiddenForFullTrace = [];

    private DockNode? _rootBeforeFullTrace;

    /// <remarks>
    /// Set while SyncTabVisibility writes the flags back from the dock, so their setters don't loop back into the dock.
    /// </remarks>
    private bool _suppressVisibilitySync;

    [ObservableProperty]
    private bool _isSqlEditorVisible = true;

    [ObservableProperty]
    private bool _isAllocationsVisible;

    [ObservableProperty]
    private bool _isExecutionPlanVisible;

    [ObservableProperty]
    private bool _isEventsVisible;

    [ObservableProperty]
    private bool _isCallstackVisible;

    [ObservableProperty]
    private bool _isInstructionsVisible;

    [ObservableProperty]
    private bool _isTimelineVisible = true;

    [ObservableProperty]
    private bool _isFlameChartVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowEventViews))]
    private bool _isFullTrace;

    /// <param name="content">
    /// The data context the tab document views bind to (the owning query view model)
    /// </param>
    public QueryLayoutViewModel(object content)
    {
        _documentsByKey = new Dictionary<string, DocumentViewModel>
        {
            [SqlKey] = DocumentViewModel.Create<QuerySqlTabView, QuerySqlTabCommands>("SQL",
                                                                                      content,
                                                                                      keepAlive: true,
                                                                                      key: SqlKey),

            [AllocationsKey] = DocumentViewModel.Create<QueryAllocationTabView, QueryAllocationTabCommands>(
                                                                                "Allocations",
                                                                                content,
                                                                                keepAlive: true,
                                                                                key: AllocationsKey),

            [PlanKey] = DocumentViewModel.Create<QueryPlanTabView, QueryPlanTabCommands>("Execution Plan",
                                                                                        content,
                                                                                        keepAlive: true,
                                                                                        key: PlanKey),

            [EventsKey] = DocumentViewModel.Create<QueryEventsTabView, QueryEventsTabCommands>("Events",
                                                                                            content,
                                                                                            keepAlive: true,
                                                                                            key: EventsKey),

            [CallstackKey] = DocumentViewModel.Create<QueryCallStackTabView>("Call Stack",
                                                                             content,
                                                                             keepAlive: true,
                                                                             key: CallstackKey),

            [InstructionsKey] = DocumentViewModel.Create<QueryInstructionsTabView>("Instructions",
                                                                                   content,
                                                                                   keepAlive: true,
                                                                                   key: InstructionsKey),

            [TimelineKey] = DocumentViewModel.Create<QueryTimelineTabView>("Timeline",
                                                                           content,
                                                                           keepAlive: true,
                                                                           key: TimelineKey),

            [FlameChartKey] = DocumentViewModel.Create<QueryFlameChartTabView>("Flame Chart",
                                                                               content,
                                                                               keepAlive: true,
                                                                               key: FlameChartKey)
        };

        Dock = new DockLayoutViewModel(DefaultRoot(TimelineKey));

        Dock.LayoutChanged += OnDockLayoutChanged;
        Dock.SelectionChanged += OnDockSelectionChanged;
    }

    /// <summary>
    /// Raised after a change that should be persisted (a tab shown/closed, the dock rearranged, timeline toggled)
    /// </summary>
    public event Action? Changed;

    public event Action? SelectionChanged;

    public DockLayoutViewModel Dock { get; }

    public bool CanShowEventViews => !IsFullTrace;

    public bool IsTimelineVisibleOutsideFullTrace
        => IsTimelineVisible || _hiddenForFullTrace.Contains(_documentsByKey[TimelineKey]);

    private DocumentViewModel[] EventViews =>
        [_documentsByKey[AllocationsKey], _documentsByKey[EventsKey], _documentsByKey[TimelineKey]];

    public DockNode SerializeRoot()
        => IsFullTrace && _rootBeforeFullTrace is { } root ? root : DockLayoutSerializer.Serialize(Dock.Root);

    public bool RestoreRoot(DockNode? dto)
    {
        var root = DockLayoutSerializer.Deserialize(dto, key => _documentsByKey.GetValueOrDefault(key));

        if (root is null)
        {
            return false;
        }

        Dock.SetRoot(root);

        Dock.Activate(_documentsByKey[SqlKey]);

        if (IsFullTrace)
        {
            HideEventViews();
        }

        return true;
    }

    /// <summary>
    /// Resets to the default SQL-over-timeline layout
    /// </summary>
    public void Reset()
    {
        if (!IsFullTrace)
        {
            Dock.SetRoot(DefaultRoot(TimelineKey));

            return;
        }

        _hiddenForFullTrace.Clear();

        _hiddenForFullTrace.Add(_documentsByKey[TimelineKey]);

        _rootBeforeFullTrace = DockLayoutSerializer.Serialize(DefaultRoot(TimelineKey));

        Dock.SetRoot(DefaultRoot(FlameChartKey));
    }

    public bool TryGetDocument(string key, out DocumentViewModel document)
        => _documentsByKey.TryGetValue(key, out document!);

    public void RegisterDocument(string key, DocumentViewModel document) => _documentsByKey[key] = document;

    public void Show(DocumentViewModel document) => Dock.Show(document);

    public void ShowExecutionPlan()
    {
        IsExecutionPlanVisible = true;

        Show(_documentsByKey[PlanKey]);
    }

    public void ShowCallStack()
    {
        IsCallstackVisible = true;

        Show(_documentsByKey[CallstackKey]);
    }

    public bool IsShown(string key) 
        => _documentsByKey.TryGetValue(key, out var document) && Dock.Contains(document);

    public bool RemoveDocument(string key, out DocumentViewModel document)
        => _documentsByKey.Remove(key, out document!);

    public void Close(DocumentViewModel document) => Dock.Close(document);

    public void Dispose()
    {
        Dock.LayoutChanged -= OnDockLayoutChanged;
        Dock.SelectionChanged -= OnDockSelectionChanged;

        foreach (var document in _documentsByKey.Values)
        {
            document.DisposeView();
        }
    }

    private LayoutNode DefaultRoot(string bottomKey)
        => new SplitNode(Orientation.Vertical,
                         new TabGroupNode(_documentsByKey[SqlKey]),
                         new TabGroupNode(_documentsByKey[bottomKey]));

    partial void OnIsSqlEditorVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[SqlKey], value);

    partial void OnIsAllocationsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[AllocationsKey], value);

    partial void OnIsExecutionPlanVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[PlanKey], value);

    partial void OnIsEventsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[EventsKey], value);

    partial void OnIsCallstackVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[CallstackKey], value);

    partial void OnIsInstructionsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[InstructionsKey], value);

    partial void OnIsTimelineVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[TimelineKey], value);

    partial void OnIsFlameChartVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[FlameChartKey], value);

    partial void OnIsFullTraceChanged(bool value)
    {
        if (value)
        {
            HideEventViews();

            return;
        }

        var flameChart = _documentsByKey[FlameChartKey];

        foreach (var document in _hiddenForFullTrace)
        {
            if (ReferenceEquals(document, _documentsByKey[TimelineKey]))
            {
                Dock.ShowBeside(document, flameChart);
            }
            else
            {
                Dock.Show(document);
            }
        }

        Dock.Close(flameChart);

        _hiddenForFullTrace.Clear();

        _rootBeforeFullTrace = null;
    }

    private void HideEventViews()
    {
        _hiddenForFullTrace.Clear();

        _rootBeforeFullTrace = DockLayoutSerializer.Serialize(Dock.Root);

        if (Dock.Contains(_documentsByKey[TimelineKey]))
        {
            Dock.ShowBeside(_documentsByKey[FlameChartKey], _documentsByKey[TimelineKey]);
        }
        else
        {
            Dock.DockBottom(_documentsByKey[FlameChartKey]);
        }

        foreach (var document in EventViews)
        {
            if (Dock.Contains(document))
            {
                _hiddenForFullTrace.Add(document);

                Dock.Close(document);
            }
        }
    }

    private void SetDocumentVisible(DocumentViewModel document, bool show)
    {
        if (_suppressVisibilitySync)
        {
            return;
        }

        if (show && IsFullTrace && Array.IndexOf(EventViews, document) >= 0)
        {
            _hiddenForFullTrace.Add(document);

            SyncTabVisibility();

            return;
        }

        if (show && ReferenceEquals(document, _documentsByKey[FlameChartKey]))
        {
            if (IsFullTrace)
            {
                Dock.DockBottom(document);
            }
            else
            {
                SyncTabVisibility();
            }

            return;
        }

        if (show)
        {
            Dock.Show(document);
        }
        else
        {
            Dock.Close(document);
        }
    }

    private void SyncTabVisibility()
    {
        _suppressVisibilitySync = true;

        IsSqlEditorVisible = Dock.Contains(_documentsByKey[SqlKey]);
        IsAllocationsVisible = Dock.Contains(_documentsByKey[AllocationsKey]);
        IsExecutionPlanVisible = Dock.Contains(_documentsByKey[PlanKey]);
        IsEventsVisible = Dock.Contains(_documentsByKey[EventsKey]);
        IsCallstackVisible = Dock.Contains(_documentsByKey[CallstackKey]);
        IsInstructionsVisible = Dock.Contains(_documentsByKey[InstructionsKey]);
        IsTimelineVisible = Dock.Contains(_documentsByKey[TimelineKey]);
        IsFlameChartVisible = Dock.Contains(_documentsByKey[FlameChartKey]);

        _suppressVisibilitySync = false;
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        SyncTabVisibility();

        Changed?.Invoke();
    }

    private void OnDockSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke();
}
