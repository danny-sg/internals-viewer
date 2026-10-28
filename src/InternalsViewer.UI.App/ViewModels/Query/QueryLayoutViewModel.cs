using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Allocation;
using InternalsViewer.UI.App.Views.Query.Events;
using InternalsViewer.UI.App.Views.Query.Instructions;
using InternalsViewer.UI.App.Views.Query.Plan;
using InternalsViewer.UI.App.Views.Query.Sql;
using InternalsViewer.UI.App.Views.Query.Timeline;
using InternalsViewer.UI.App.Views.Query.Trace.CallStack;
using InternalsViewer.UI.App.Views.Query.Trace.CustomHooks;
using InternalsViewer.UI.App.Views.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.Views.Query.Trace.OperatorTracks;
using InternalsViewer.UI.App.Views.Query.Trace.Timeline;
using Microsoft.UI.Xaml.Controls;
using QueryPlanTabCommands = InternalsViewer.UI.App.Views.Query.Plan.QueryPlanTabCommands;

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
    private const string CallTreeKey = "CallTree";
    private const string LegacyCallTreeKey = "Callstack";
    private const string InstructionsKey = "Instructions";
    private const string TimelineKey = "Timeline";
    private const string TraceTimelineKey = "FlameChart";
    private const string CustomHooksKey = "CustomHooks";
    private const string OperatorTracksKey = "OperatorTracks";
    private const string MemoryHistoryKey = "MemoryHistory";

    private readonly Dictionary<string, DocumentViewModel> _documentsByKey;

    private readonly HashSet<DocumentViewModel> _hiddenForTrace = [];

    private DockNode? _rootBeforeTrace;

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
    private bool _isCallTreeVisible;

    [ObservableProperty]
    private bool _isCallTreeMembersVisible;

    [ObservableProperty]
    private bool _isCallTreeSymbolsVisible;

    [ObservableProperty]
    private bool _isInstructionsVisible;

    [ObservableProperty]
    private bool _isTimelineVisible = true;

    [ObservableProperty]
    private bool _isTraceTimelineVisible;

    [ObservableProperty]
    private bool _isCustomHooksVisible;

    [ObservableProperty]
    private bool _isOperatorTracksVisible;

    [ObservableProperty]
    private bool _isMemoryHistoryVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowEventViews))]
    private bool _isTrace;

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

            [CallTreeKey] = DocumentViewModel.Create<QueryCallTreeTabView>("Call Tree",
                                                                             content,
                                                                             keepAlive: true,
                                                                             key: CallTreeKey),

            [InstructionsKey] = DocumentViewModel.Create<QueryInstructionsTabView>("Instructions",
                                                                                   content,
                                                                                   keepAlive: true,
                                                                                   key: InstructionsKey),

            [TimelineKey] = DocumentViewModel.Create<QueryTimelineTabView>("Timeline",
                                                                           content,
                                                                           keepAlive: true,
                                                                           key: TimelineKey),

            [TraceTimelineKey] = DocumentViewModel.Create<QueryTraceTimelineTabView>("Trace Timeline",
                                                                               content,
                                                                               keepAlive: true,
                                                                               key: TraceTimelineKey),

            [CustomHooksKey] = DocumentViewModel.Create<QueryCustomHooksTabView>("Custom Hooks",
                                                                                 content,
                                                                                 keepAlive: true,
                                                                                 key: CustomHooksKey),

            [OperatorTracksKey] = DocumentViewModel.Create<QueryOperatorTracksTabView>("Operator Tracks",
                                                                                       content,
                                                                                       keepAlive: true,
                                                                                       key: OperatorTracksKey),

            [MemoryHistoryKey] = DocumentViewModel.Create<QueryMemoryHistoryTabView>("Memory History",
                                                                                     content,
                                                                                     keepAlive: true,
                                                                                     key: MemoryHistoryKey)
        };

        Dock = new DockLayoutViewModel(DefaultRoot());

        Dock.LayoutChanged += OnDockLayoutChanged;
        Dock.SelectionChanged += OnDockSelectionChanged;
    }

    /// <summary>
    /// Raised after a change that should be persisted (a tab shown/closed, the dock rearranged, timeline toggled)
    /// </summary>
    public event Action? Changed;

    public event Action? SelectionChanged;

    public DockLayoutViewModel Dock { get; }

    public bool CanShowEventViews => !IsTrace;

    public bool IsTimelineVisibleOutsideTrace
        => IsTimelineVisible || _hiddenForTrace.Contains(_documentsByKey[TimelineKey]);

    private DocumentViewModel[] EventViews =>
        [_documentsByKey[AllocationsKey], _documentsByKey[EventsKey], _documentsByKey[TimelineKey]];

    public DockNode SerializeRoot()
        => IsTrace && _rootBeforeTrace is { } root ? root : DockLayoutSerializer.Serialize(Dock.Root);

    public bool RestoreRoot(DockNode? dto)
    {
        var root = DockLayoutSerializer.Deserialize(dto,
                                                    key => _documentsByKey.GetValueOrDefault(key == LegacyCallTreeKey ? CallTreeKey : key));

        if (root is null)
        {
            return false;
        }

        Dock.SetRoot(root);

        Dock.Activate(_documentsByKey[SqlKey]);

        if (IsTrace)
        {
            EnterTrace();
        }

        return true;
    }

    /// <summary>
    /// Resets to the default SQL-over-timeline layout
    /// </summary>
    public void Reset()
    {
        if (!IsTrace)
        {
            Dock.SetRoot(DefaultRoot());

            return;
        }

        _hiddenForTrace.Clear();

        _rootBeforeTrace = DockLayoutSerializer.Serialize(DefaultRoot());

        _hiddenForTrace.Add(_documentsByKey[TimelineKey]);

        Dock.SetRoot(new TabGroupNode(_documentsByKey[SqlKey], _documentsByKey[TraceTimelineKey]));

        Dock.Activate(_documentsByKey[TraceTimelineKey]);
    }

    public bool TryGetDocument(string key, out DocumentViewModel document)
        => _documentsByKey.TryGetValue(key, out document!);

    public void RegisterDocument(string key, DocumentViewModel document) => _documentsByKey[key] = document;

    public void Show(DocumentViewModel document) => Dock.Show(document);

    public void ShowSqlEditor()
    {
        IsSqlEditorVisible = true;

        Show(_documentsByKey[SqlKey]);
    }

    public void ShowExecutionPlan()
    {
        IsExecutionPlanVisible = true;

        Show(_documentsByKey[PlanKey]);
    }

    public void ShowCallTree()
    {
        IsCallTreeVisible = true;

        Show(_documentsByKey[CallTreeKey]);
    }

    public void ShowTraceTimeline()
    {
        IsTraceTimelineVisible = true;

        Show(_documentsByKey[TraceTimelineKey]);
    }

    public void ShowCustomHooks()
    {
        IsCustomHooksVisible = true;

        Show(_documentsByKey[CustomHooksKey]);
    }

    public void ShowOperatorTracks()
    {
        IsOperatorTracksVisible = true;

        Show(_documentsByKey[OperatorTracksKey]);
    }

    public void ShowMemoryHistory()
    {
        IsMemoryHistoryVisible = true;

        Show(_documentsByKey[MemoryHistoryKey]);
    }

    public bool IsVisible(string key)
        => _documentsByKey.TryGetValue(key, out var document) && Dock.Contains(document);

    public IEnumerable<string> VisibleKeys => _documentsByKey.Where(d => Dock.Contains(d.Value)).Select(d => d.Key);

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

    private LayoutNode DefaultRoot()
        => new SplitNode(Orientation.Vertical,
                         new TabGroupNode(_documentsByKey[SqlKey]),
                         new TabGroupNode(_documentsByKey[TimelineKey]));

    partial void OnIsSqlEditorVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[SqlKey], value);

    partial void OnIsAllocationsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[AllocationsKey], value);

    partial void OnIsExecutionPlanVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[PlanKey], value);

    partial void OnIsEventsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[EventsKey], value);

    partial void OnIsCallTreeVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[CallTreeKey], value);

    partial void OnIsCallTreeMembersVisibleChanged(bool value) => IsCallTreeVisible |= value;

    partial void OnIsCallTreeSymbolsVisibleChanged(bool value) => IsCallTreeVisible |= value;

    partial void OnIsInstructionsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[InstructionsKey], value);

    partial void OnIsTimelineVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[TimelineKey], value);

    partial void OnIsTraceTimelineVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[TraceTimelineKey], value);

    partial void OnIsCustomHooksVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[CustomHooksKey], value);

    partial void OnIsOperatorTracksVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[OperatorTracksKey], value);

    partial void OnIsMemoryHistoryVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[MemoryHistoryKey], value);

    partial void OnIsTraceChanged(bool value)
    {
        if (value)
        {
            EnterTrace();

            return;
        }

        RestoreEventViews();

        Dock.Close(_documentsByKey[TraceTimelineKey]);

        _rootBeforeTrace = null;
    }

    private void EnterTrace()
    {
        _hiddenForTrace.Clear();

        _rootBeforeTrace = DockLayoutSerializer.Serialize(Dock.Root);

        ShowTraceTimelineTab(select: false);

        HideEventViews();
    }

    private void ShowTraceTimelineTab(bool select)
    {
        var document = _documentsByKey[TraceTimelineKey];

        if (Dock.Groups().Any())
        {
            Dock.ShowBeside(document, _documentsByKey[SqlKey], select);
        }
        else
        {
            Dock.SetRoot(new TabGroupNode(document));
        }
    }

    private void RestoreEventViews()
    {
        foreach (var document in _hiddenForTrace)
        {
            if (ReferenceEquals(document, _documentsByKey[TimelineKey]))
            {
                Dock.DockBottom(document);
            }
            else
            {
                Dock.Show(document);
            }
        }

        _hiddenForTrace.Clear();
    }

    private void HideEventViews()
    {
        foreach (var document in EventViews)
        {
            if (Dock.Contains(document))
            {
                _hiddenForTrace.Add(document);

                Dock.Close(document);
            }
        }
    }

    private void SetDocumentVisible(DocumentViewModel document, bool isVisible)
    {
        if (_suppressVisibilitySync)
        {
            return;
        }

        if (isVisible && !CanShowEventViews && Array.IndexOf(EventViews, document) >= 0)
        {
            _hiddenForTrace.Add(document);

            SyncTabVisibility();

            return;
        }

        if (isVisible && ReferenceEquals(document, _documentsByKey[TraceTimelineKey]))
        {
            if (IsTrace)
            {
                ShowTraceTimelineTab(select: true);
            }
            else
            {
                SyncTabVisibility();
            }

            return;
        }

        if (isVisible)
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
        IsCallTreeVisible = Dock.Contains(_documentsByKey[CallTreeKey]);
        IsInstructionsVisible = Dock.Contains(_documentsByKey[InstructionsKey]);
        IsTimelineVisible = Dock.Contains(_documentsByKey[TimelineKey]);
        IsTraceTimelineVisible = Dock.Contains(_documentsByKey[TraceTimelineKey]);
        IsCustomHooksVisible = Dock.Contains(_documentsByKey[CustomHooksKey]);
        IsOperatorTracksVisible = Dock.Contains(_documentsByKey[OperatorTracksKey]);
        IsMemoryHistoryVisible = Dock.Contains(_documentsByKey[MemoryHistoryKey]);

        _suppressVisibilitySync = false;
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        SyncTabVisibility();

        Changed?.Invoke();
    }

    private void OnDockSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke();
}
