using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs;
using InternalsViewer.UI.App.Views.Query.Tabs.CallStack;
using InternalsViewer.UI.App.Views.Query.Tabs.Timeline;
using Microsoft.UI.Xaml.Controls;
using QueryPlanTabCommands = InternalsViewer.UI.App.Views.Query.Tabs.Plan.QueryPlanTabCommands;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// Owns the query view's dock layout — the tab documents, their menu-driven visibility, and the timeline/details rows
/// </summary>
/// <remarks>
/// The tab-visibility flags are two-way bound to the View menu and kept in step with the dock: toggling a flag shows or
/// closes the document, and rearranging the dock re-syncs the flags. <see cref="Changed"/> is raised after any change
/// that should be persisted; the owner handles pruning transient tabs and scheduling the save.
/// </remarks>
public sealed partial class QueryLayoutViewModel : ObservableObject, IDisposable
{
    private const string SqlKey = "Sql";
    private const string AllocationsKey = "Allocations";
    private const string PlanKey = "Plan";
    private const string EventsKey = "Events";
    private const string CallstackKey = "Callstack";
    private const string InstructionsKey = "Instructions";
    private const string TimelineKey = "Timeline";

    private readonly Dictionary<string, DocumentViewModel> _documentsByKey;

    // Set while SyncTabVisibility writes the flags back from the dock, so their setters don't loop back into the dock.
    private bool _suppressVisibilitySync;

    /// <summary>Raised after a change that should be persisted (a tab shown/closed, the dock rearranged, timeline toggled)</summary>
    public event Action? Changed;

    public event Action? SelectionChanged;

    public DockLayoutViewModel Dock { get; }

    /// <param name="content">
    /// The data context the tab document views bind to (the owning query view model).
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

            [EventsKey] = DocumentViewModel.Create<QueryEventsTabView>("Events",
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
                                                                           key: TimelineKey)
        };

        Dock = new DockLayoutViewModel(DefaultRoot());

        Dock.LayoutChanged += OnDockLayoutChanged;
        Dock.SelectionChanged += OnDockSelectionChanged;
    }

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

    /// <summary>
    /// The default layout: the SQL editor above the timeline, which is where the timeline sat when it was a fixed
    /// row rather than a document
    /// </summary>
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

    partial void OnIsCallstackVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[CallstackKey], value);

    partial void OnIsInstructionsVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[InstructionsKey], value);

    partial void OnIsTimelineVisibleChanged(bool value)
        => SetDocumentVisible(_documentsByKey[TimelineKey], value);

    /// <summary>
    /// Serialises the current dock tree for persistence
    /// </summary>
    public DockNode SerializeRoot() => DockLayoutSerializer.Serialize(Dock.Root);

    public bool RestoreRoot(DockNode? dto)
    {
        var root = DockLayoutSerializer.Deserialize(dto, key => _documentsByKey.GetValueOrDefault(key));

        if (root is null)
        {
            return false;
        }

        Dock.SetRoot(root);

        return true;
    }

    /// <summary>
    /// Resets to the default SQL-over-timeline layout
    /// </summary>
    public void Reset() => Dock.SetRoot(DefaultRoot());

    /// <summary>
    /// Look up an already-open document (base tab or index tab) by key
    /// </summary>
    public bool TryGetDocument(string key, out DocumentViewModel document)
        => _documentsByKey.TryGetValue(key, out document!);

    /// <summary>
    /// Registers a transient document by key
    /// </summary>
    public void RegisterDocument(string key, DocumentViewModel document) => _documentsByKey[key] = document;

    /// <summary>
    /// Show a document in the dock, creating or focusing its tab
    /// </summary>
    public void Show(DocumentViewModel document) => Dock.Show(document);

    public void ShowExecutionPlan()
    {
        IsExecutionPlanVisible = true;

        Show(_documentsByKey[PlanKey]);
    }

    /// <summary>
    /// Whether the keyed document is currently present in the dock
    /// </summary>
    public bool IsShown(string key) 
        => _documentsByKey.TryGetValue(key, out var document) && Dock.Contains(document);

    /// <summary>
    /// Drops a transient document from tracking, returning it so the caller can dispose its view
    /// </summary>
    public bool RemoveDocument(string key, out DocumentViewModel document) 
        => _documentsByKey.Remove(key, out document!);

    private void SetDocumentVisible(DocumentViewModel document, bool show)
    {
        if (_suppressVisibilitySync)
        {
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

        _suppressVisibilitySync = false;
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        SyncTabVisibility();

        Changed?.Invoke();
    }

    private void OnDockSelectionChanged(object? sender, EventArgs e) => SelectionChanged?.Invoke();

    public void Dispose()
    {
        Dock.LayoutChanged -= OnDockLayoutChanged;
        Dock.SelectionChanged -= OnDockSelectionChanged;

        foreach (var document in _documentsByKey.Values)
        {
            document.DisposeView();
        }
    }
}
