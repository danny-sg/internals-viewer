using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// Owns the query view's dock layout — the tab documents, their menu-driven visibility, and the timeline/details rows
/// </summary>
/// <remarks>
/// The tab-visibility flags are two-way bound to the View menu and kept in step with the dock: toggling a flag shows or
/// closes the document, and rearranging the dock re-syncs the flags. <see cref="Changed"/> is raised after any change
/// that should be persisted; the owner handles pruning transient tabs and scheduling the save.
/// </remarks>
public sealed partial class QueryLayoutViewModel : ObservableObject
{
    private const string SqlKey = "Sql";
    private const string AllocationsKey = "Allocations";
    private const string PlanKey = "Plan";
    private const string EventsKey = "Events";
    private const string CallstackKey = "Callstack";
    private const string InstructionsKey = "Instructions";

    private readonly Dictionary<string, DocumentViewModel> _documentsByKey;

    // Set while SyncTabVisibility writes the flags back from the dock, so their setters don't loop back into the dock.
    private bool _suppressVisibilitySync;

    /// <summary>Raised after a change that should be persisted (a tab shown/closed, the dock rearranged, timeline toggled)</summary>
    public event Action? Changed;

    public DockLayoutViewModel Dock { get; }

    /// <param name="content">The data context the tab document views bind to (the owning query view model).</param>
    public QueryLayoutViewModel(object content)
    {
        _documentsByKey = new Dictionary<string, DocumentViewModel>
        {
            [SqlKey] = DocumentViewModel.Create<SqlDocumentView>("SQL",
                                                                 content,
                                                                 keepAlive: true,
                                                                 key: SqlKey),

            [AllocationsKey] = DocumentViewModel.Create<AllocationDocumentView>("Allocations",
                                                                                content,
                                                                                keepAlive: true,
                                                                                key: AllocationsKey),

            [PlanKey] = DocumentViewModel.Create<PlanDocumentView>("Execution Plan",
                                                                   content,
                                                                   keepAlive: true,
                                                                   key: PlanKey),

            [EventsKey] = DocumentViewModel.Create<EventsDocumentView>("Events",
                                                                       content,
                                                                       keepAlive: true,
                                                                       key: EventsKey),

            [CallstackKey] = DocumentViewModel.Create<CallstackDocumentView>("Call Stack",
                                                                             content,
                                                                             keepAlive: true,
                                                                             key: CallstackKey),

            [InstructionsKey] = DocumentViewModel.Create<InstructionsDocumentView>("Instructions", 
                                                                                   content, 
                                                                                   keepAlive: true, 
                                                                                   key: InstructionsKey)
        };

        Dock = new DockLayoutViewModel(new TabGroupNode(_documentsByKey[SqlKey]));

        Dock.LayoutChanged += OnDockLayoutChanged;
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
    [NotifyPropertyChangedFor(nameof(TimelineRowHeight))]
    [NotifyPropertyChangedFor(nameof(SplitterVisibility))]
    private bool _isTimelineVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsRowHeight))]
    [NotifyPropertyChangedFor(nameof(SplitterVisibility))]
    private bool _isDetailsVisible = true;

    public GridLength TimelineRowHeight
        => IsTimelineVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength DetailsRowHeight
        => IsDetailsVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Visibility SplitterVisibility
        => (IsTimelineVisible && IsDetailsVisible) ? Visibility.Visible : Visibility.Collapsed;

    partial void OnIsSqlEditorVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[SqlKey], value);

    partial void OnIsAllocationsVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[AllocationsKey], value);

    partial void OnIsExecutionPlanVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[PlanKey], value);

    partial void OnIsEventsVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[EventsKey], value);

    partial void OnIsCallstackVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[CallstackKey], value);

    partial void OnIsInstructionsVisibleChanged(bool value) => SetDocumentVisible(_documentsByKey[InstructionsKey], value);

    partial void OnIsTimelineVisibleChanged(bool value) => Changed?.Invoke();

    /// <summary>Serialises the current dock tree for persistence</summary>
    public DockNode SerializeRoot() => DockLayoutSerializer.Serialize(Dock.Root);

    /// <summary>Restores a persisted dock tree; returns false (leaving the layout untouched) if it can't be rebuilt</summary>
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

    /// <summary>Resets to the default single-tab (SQL) layout with the timeline shown</summary>
    public void Reset()
    {
        IsTimelineVisible = true;

        Dock.SetRoot(new TabGroupNode(_documentsByKey[SqlKey]));
    }

    /// <summary>Looks up an already-open document (base tab or index tab) by key</summary>
    public bool TryGetDocument(string key, out DocumentViewModel document)
        => _documentsByKey.TryGetValue(key, out document!);

    /// <summary>Registers a transient document (an index tab) so the dock and pruning can track it by key</summary>
    public void RegisterDocument(string key, DocumentViewModel document) => _documentsByKey[key] = document;

    /// <summary>Shows a document in the dock, creating or focusing its tab</summary>
    public void Show(DocumentViewModel document) => Dock.Show(document);

    /// <summary>Whether the keyed document is currently present in the dock</summary>
    public bool IsShown(string key) => _documentsByKey.TryGetValue(key, out var document) && Dock.Contains(document);

    /// <summary>Drops a transient document from tracking, returning it so the caller can dispose its view</summary>
    public bool RemoveDocument(string key, out DocumentViewModel document) => _documentsByKey.Remove(key, out document!);

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

        _suppressVisibilitySync = false;
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        SyncTabVisibility();

        Changed?.Invoke();
    }
}
