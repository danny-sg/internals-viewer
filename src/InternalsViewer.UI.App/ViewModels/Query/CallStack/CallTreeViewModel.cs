using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class CallTreeViewModel : ObservableObject, IDisposable
{
    private const int ExpandedLeaves = 100;

    private const int SearchExpandBudget = 2000;

    [ObservableProperty]
    private CallTreeLayout _layout = CallTreeLayout.Empty;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private bool _isFocused = true;

    [ObservableProperty]
    private bool _isActivityShown = true;

    [ObservableProperty]
    private bool _isSignatureShown;

    [ObservableProperty]
    private string _highlight = string.Empty;

    public CallTreeViewModel(CallTreeTabViewModel owner, SettingsViewModel settings)
    {
        Owner = owner;
        Settings = settings;

        Query.PropertyChanged += OnQueryPropertyChanged;
        Query.CallNavigationRequested += OnCallNavigationRequested;
        Query.Arguments.CallSelected += OnArgumentCallSelected;
        Query.QueryOptions.PropertyChanged += OnQueryOptionsPropertyChanged;
        Query.QueryOptions.FilterChanged += OnQueryFilterChanged;
        Settings.PropertyChanged += OnSettingsPropertyChanged;

        ApplyFocus(Query.SelectedEvent);
    }

    public event Action<CallTreeRow>? RowSelectionRequested;

    public event Action<string>? SearchRequested;

    public CallStackTree? CallStack => Query.CallStack;

    private CallTreeTabViewModel Owner { get; }

    private QueryViewModel Query => Owner.Query;

    private SettingsViewModel Settings { get; }

    private List<CallTreeLocation> History { get; } = [];

    private int HistoryIndex { get; set; } = -1;

    private bool IsNavigatingHistory { get; set; }

    private bool IsSelectingFromTree { get; set; }

    private CallStackNode? NavigatedNode { get; set; }

    private CallStackTree? SignaturesFor { get; set; }

    public bool CanShowArguments(CallStackNode node)
        => Query is { CallStack.ActivityFromTrace: true } && Query.Arguments.CanShow(node);

    public void SetFocused(bool focused)
    {
        IsFocused = focused;

        ClearReveal();

        ApplyFocus(Query.SelectedEvent);
    }

    public void SetActivityShown(bool shown)
    {
        IsActivityShown = shown;

        ApplyFocus(Query.SelectedEvent, keepSelection: true);
    }

    public async Task SetSignatureShownAsync(bool shown)
    {
        IsSignatureShown = shown;

        await RefreshSignaturesAsync();
    }

    public void SetSearch(string text)
    {
        ApplySearch(text);

        ApplyFocus(Query.SelectedEvent, keepSelection: true);
    }

    public void RequestSearch(string text) => SearchRequested?.Invoke(text);

    public void GoBack() => GoTo(HistoryIndex - 1);

    public void GoForward() => GoTo(HistoryIndex + 1);

    public void Reset()
    {
        LeaveLocation();

        FilterPath = null;

        ClearReveal();

        SelectedPath = null;

        ApplySearch(string.Empty);

        SelectQuietly(null);

        Query.SelectedCallNode = null;

        ApplyFocus(null);

        PushLocation(CurrentLocation());
    }

    public void FilterTo(CallStackNode node)
    {
        FilterPath = GetPath(node);

        ApplyFilter();

        Publish();
    }

    public void ClearFilter()
    {
        FilterPath = null;

        ApplyFocus(Query.SelectedEvent, keepSelection: true);
    }

    public void ListMembers(CallStackNode node) => Owner.ListMembers(node);

    public void ShowArguments(CallStackNode node, int? call = null)
    {
        Owner.ShowArguments();

        _ = call is { } index ? Query.Arguments.ShowAsync(node, index) : Query.Arguments.ShowAsync(node);
    }

    public void ShowCall(CallStackNode node, int call)
    {
        LeaveLocation();

        NavigateTo(node);

        PushLocation(new CallTreeLocation(Query.SelectedEvent, node, IsFocused, node, call));

        _ = Query.Arguments.ShowAsync(node, call);
    }

    public void GoToCall(CallStackNode node)
    {
        LeaveLocation();

        NavigateTo(node, selectInFlameChart: false);

        PushLocation(CurrentLocation());
    }

    public void ShowOrigin(MemoryMapEntry origin)
    {
        if (Query.Arguments.Lookup?.GetOrigin(origin) is not { } reference)
        {
            return;
        }

        if (reference.Call >= 0)
        {
            ShowCall(reference.Node, reference.Call);
        }
        else
        {
            GoToCall(reference.Node);
        }
    }

    public void NavigateToIterator(IteratorTarget target)
    {
        LeaveLocation();

        if (target.Operator is { } operatorEvent)
        {
            SelectQuietly(operatorEvent);
        }

        NavigateTo(target.Node);

        PushLocation(CurrentLocation());
    }

    // Following the header's parent link, like following one in the tree: a request to go there, so it rebuilds around
    // the parent rather than leaving the current segment on screen with the selection moved out from under it.
    public void FollowParent()
    {
        if (Layout.FocusParent is { } link)
        {
            Query.SelectedEvent = link.Operator;
        }
    }

    // Clicking a frame selects the event it came from, so the grid, details and timeline all answer "what is this frame
    // doing here?" — the question the tree cannot answer on its own, and the one every diagnosis of the scoping has
    // needed. A frame with no events of its own reports the earliest beneath it: that is still the work it led to.
    public void Invoke(object content)
    {
        if (content is CallStackNode clicked)
        {
            Query.SelectedCallNode = clicked;

            SelectedPath = GetPath(clicked);

            if (RevealTarget is not null && !IsRevealed(clicked))
            {
                Unreveal();
            }

            if (CanShowArguments(clicked))
            {
                ShowArguments(clicked);
            }
        }

        var selected = content switch
        {
            OperatorLink link => link.Operator,
            OperatorRow row => row.Operator,
            CallStackNode frame => EarliestEvent(frame),
            _ => null,
        };

        if (selected is null)
        {
            return;
        }

        // A hand-off link is a request to GO somewhere, so it must rebuild — that is the whole of what it does. Every
        // other row is just reporting what it already shows, and rebuilding under the click would throw away the
        // expansion the user opened to get there.
        var navigating = content is OperatorLink;

        IsSelectingFromTree = !navigating;

        try
        {
            Query.SelectedEvent = selected;
        }
        finally
        {
            IsSelectingFromTree = false;
        }
    }

    public void Dispose()
    {
        Query.PropertyChanged -= OnQueryPropertyChanged;
        Query.CallNavigationRequested -= OnCallNavigationRequested;
        Query.Arguments.CallSelected -= OnArgumentCallSelected;
        Query.QueryOptions.PropertyChanged -= OnQueryOptionsPropertyChanged;
        Query.QueryOptions.FilterChanged -= OnQueryFilterChanged;
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    private void OnCallNavigationRequested(CallStackNode node, int call)
    {
        int? index = call >= 0 ? call : null;

        LeaveLocation();

        NavigateTo(node);

        PushLocation(new CallTreeLocation(Query.SelectedEvent, node, IsFocused, node, index));

        if (CanShowArguments(node))
        {
            ShowArguments(node, index);
        }
    }

    private void OnArgumentCallSelected(CallReference reference)
    {
        if (IsFocused || !Rows.TryGetValue(reference.Node, out var row) || (RevealTarget is not null && !IsRevealed(reference.Node)))
        {
            GoToCall(reference.Node);

            return;
        }

        SelectRow(row, reference.Node);
    }

    private void OnQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new query's events are new objects, so nowhere the history points at exists any more.
        if (e.PropertyName == nameof(QueryViewModel.CallStack))
        {
            ClearHistory();

            FilterPath = null;

            ClearReveal();

            SelectedPath = null;

            ApplyFocus(Query.SelectedEvent);

            OnPropertyChanged(nameof(CallStack));

            if (IsSignatureShown)
            {
                _ = RefreshSignaturesAsync();
            }
        }
        else if (e.PropertyName == nameof(QueryViewModel.SelectedEvent) && !IsSelectingFromTree)
        {
            RecordHistory(Query.SelectedEvent);

            ClearReveal();

            ApplyFocus(Query.SelectedEvent);
        }
    }

    private void OnQueryOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QueryOptionsViewModel.ShowLatches) or nameof(QueryOptionsViewModel.CropToQuery))
        {
            ApplyFocus(Query.SelectedEvent, keepSelection: true);
        }
    }

    private void OnQueryFilterChanged() => ApplyFocus(Query.SelectedEvent, keepSelection: true);

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.CallTreeHiddenCategories) or nameof(SettingsViewModel.CallTreePrunedCategories))
        {
            ApplyFocus(Query.SelectedEvent, keepSelection: true);
        }
    }

    private async Task RefreshSignaturesAsync()
    {
        if (IsSignatureShown && Query.CallStack is { } tree && !ReferenceEquals(SignaturesFor, tree))
        {
            SignaturesFor = tree;

            await Query.Symbols.ResolveFrameSignaturesAsync([.. tree.Nodes().Select(n => n.Frame).OfType<CallstackFrame>()]);
        }

        ApplyFocus(Query.SelectedEvent, keepSelection: true);
    }

    private void GoTo(int index)
    {
        if (index < 0 || index >= History.Count)
        {
            return;
        }

        SnapshotLocation();

        HistoryIndex = index;

        IsNavigatingHistory = true;

        try
        {
            Restore(History[index]);
        }
        finally
        {
            IsNavigatingHistory = false;
        }

        UpdateHistory();
    }

    private void Restore(CallTreeLocation location)
    {
        IsFocused = location.Focus;

        SelectQuietly(location.Event);

        if (location.Node is { } node)
        {
            NavigateTo(node);
        }
        else
        {
            ApplyFocus(location.Event);
        }

        if (location.Function is { } function)
        {
            _ = Query.Arguments.ShowAsync(function, location.Call);
        }
    }

    private void SelectQuietly(EngineEvent? selected)
    {
        if (ReferenceEquals(Query.SelectedEvent, selected))
        {
            return;
        }

        IsSelectingFromTree = true;

        try
        {
            Query.SelectedEvent = selected;
        }
        finally
        {
            IsSelectingFromTree = false;
        }
    }

    private CallTreeLocation CurrentLocation()
        => new(Query.SelectedEvent, NavigatedNode, IsFocused, Query.Arguments.Shown, Query.Arguments.SelectedCallIndex);

    private void SnapshotLocation()
    {
        if (HistoryIndex < 0)
        {
            return;
        }

        History[HistoryIndex] = History[HistoryIndex] with
        {
            Focus = IsFocused,
            Function = Query.Arguments.Shown,
            Call = Query.Arguments.SelectedCallIndex
        };
    }

    private void LeaveLocation()
    {
        if (HistoryIndex < 0)
        {
            PushLocation(CurrentLocation());

            return;
        }

        SnapshotLocation();
    }

    private void PushLocation(CallTreeLocation location)
    {
        History.RemoveRange(HistoryIndex + 1, History.Count - HistoryIndex - 1);

        History.Add(location);

        HistoryIndex = History.Count - 1;

        UpdateHistory();
    }

    /// <summary>
    /// Records a selection as a place that can be returned to
    /// </summary>
    private void RecordHistory(EngineEvent? selected)
    {
        if (selected is null || IsNavigatingHistory)
        {
            return;
        }

        if (HistoryIndex >= 0 && History[HistoryIndex] is { Node: null } current && ReferenceEquals(current.Event, selected))
        {
            return;
        }

        SnapshotLocation();

        PushLocation(new CallTreeLocation(selected, null, IsFocused, Query.Arguments.Shown, Query.Arguments.SelectedCallIndex));
    }

    private void ClearHistory()
    {
        History.Clear();

        HistoryIndex = -1;

        UpdateHistory();
    }

    private void UpdateHistory()
    {
        CanGoBack = HistoryIndex > 0;

        CanGoForward = HistoryIndex >= 0 && HistoryIndex < History.Count - 1;
    }

    private void NavigateTo(CallStackNode target, bool selectInFlameChart = true)
    {
        if (!IsFocused && Rows.TryGetValue(target, out var row) && (RevealTarget is null || IsRevealed(target)))
        {
            SelectRow(row, target);
        }
        else
        {
            IsFocused = false;

            if (FilterPath is not null && !target.Ancestors().Any(a => GetPath(a) == FilterPath))
            {
                FilterPath = null;
            }

            RevealPath(target);

            BuildTree();

            ApplyFilter();

            SelectNode(target);

            ApplyActivityBands();

            Publish();
        }

        NavigatedNode = target;

        if (selectInFlameChart)
        {
            Query.SelectedCallNode = target;
        }
    }

    private void SelectRow(CallTreeRow row, CallStackNode node)
    {
        Selected = row;

        SelectedPath = GetPath(node);

        RowSelectionRequested?.Invoke(row);
    }

    private void Unreveal()
    {
        ClearReveal();

        BuildTree();

        ApplyFilter();

        KeepSelection();

        ApplyActivityBands();

        Publish();
    }

    private static EngineEvent? EarliestEvent(CallStackNode node)
    {
        EngineEvent? earliest = null;

        var pending = new Stack<CallStackNode>();

        pending.Push(node);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var engineEvent in current.Events)
            {
                if (earliest is null || engineEvent.SequenceId < earliest.SequenceId)
                {
                    earliest = engineEvent;
                }
            }

            foreach (var child in current.ChildNodes)
            {
                pending.Push(child);
            }
        }

        return earliest;
    }

    private void ApplySearch(string text)
    {
        Search = text.Trim();

        SearchedLocator = LocatorSearch.TryParse(Search, out var locator) ? locator : null;

        Highlight = SearchedLocator is null ? Search : string.Empty;
    }
}
