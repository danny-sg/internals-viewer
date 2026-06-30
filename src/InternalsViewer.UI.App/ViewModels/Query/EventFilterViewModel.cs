using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.UI.App.Services;
using Microsoft.UI.Dispatching;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// Owns the engine-event filter: the checkable event/type tree, the "include system objects"
/// toggle, persistence of the unchecked set, and applying the filter to a set of events.
/// Raises <see cref="FilterChanged"/> (debounced) whenever the effective filter changes.
/// </summary>
public sealed partial class EventFilterViewModel : ObservableObject
{
    private const string UncheckedSettingsKey = "EventFilterUnchecked";

    private static readonly HashSet<string> DefaultUnchecked =
    [
        "lock_acquired/SCH_M/Metadata",
        "lock_acquired/SCH_S/Metadata",
        "lock_acquired/SCH_S/Object",
        "lock_acquired/S/Database",
        "lock_released/SCH_M/Metadata",
        "lock_released/SCH_S/Metadata",
        "lock_released/SCH_S/Object",
        "lock_released/S/Database",
    ];

    private readonly SettingsService settingsService;
    private readonly DispatcherQueue dispatcherQueue;

    private HashSet<int> systemObjectIds = [];

    private bool filterRefreshPending;
    private bool saveUncheckedPending;

    [ObservableProperty]
    private ObservableCollection<EventFilterNode> filterNodes = [];

    [ObservableProperty]
    private bool includeSystemObjects;

    /// <summary>Raised when the effective filter changes; the owner re-applies it to its events.</summary>
    public event Action? FilterChanged;

    public EventFilterViewModel(SettingsService settingsService)
    {
        this.settingsService = settingsService;
        dispatcherQueue = DispatcherQueue.GetForCurrentThread()
                          ?? throw new InvalidOperationException(
                              $"{nameof(EventFilterViewModel)} must be constructed on the UI thread.");
    }

    /// <summary>System object ids excluded when <see cref="IncludeSystemObjects"/> is off.</summary>
    public void SetSystemObjectIds(HashSet<int> ids) => systemObjectIds = ids;

    partial void OnIncludeSystemObjectsChanged(bool value) => FilterChanged?.Invoke();

    /// <summary>Clears the filter tree (e.g. when results are cleared).</summary>
    public void Clear() => FilterNodes = [];

    /// <summary>Builds the event/type tree from <paramref name="events"/>, restoring the saved unchecked set.</summary>
    public async Task BuildAsync(IReadOnlyCollection<EngineEvent> events)
    {
        var savedUnchecked = await settingsService.ReadSettingAsync<List<string>>(UncheckedSettingsKey);

        var uncheckedPaths = savedUnchecked is { Count: > 0 }
            ? new HashSet<string>(savedUnchecked)
            : DefaultUnchecked;

        var root = new EventFilterNode { Label = "Events" };

        var eventNodes = events
            .GroupBy(e => e.Name)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var eventNode = new EventFilterNode { Label = g.Key, Parent = root };

                var children = g
                    .Select(e => e.Description)
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .Select(d =>
                    {
                        var path = $"{g.Key}/{d}";
                        var child = new EventFilterNode
                        {
                            Label = d,
                            Parent = eventNode,
                            IsChecked = !uncheckedPaths.Contains(path)
                        };
                        child.PropertyChanged += OnFilterNodeChanged;
                        return child;
                    });

                foreach (var child in children)
                {
                    eventNode.Children.Add(child);
                }

                eventNode.RefreshCheckedState();
                eventNode.PropertyChanged += OnFilterNodeChanged;

                return eventNode;
            });

        foreach (var node in eventNodes)
        {
            root.Children.Add(node);
        }

        root.RefreshCheckedState();
        root.PropertyChanged += OnFilterNodeChanged;

        FilterNodes = new ObservableCollection<EventFilterNode> { root };

        FilterChanged?.Invoke();
    }

    /// <summary>Applies the system-object and tree filters to <paramref name="events"/>.</summary>
    public IEnumerable<EngineEvent> Apply(IReadOnlyCollection<EngineEvent> events)
    {
        var source = !IncludeSystemObjects && systemObjectIds.Count > 0
            ? events.Where(e => e.ObjectId == 0 || !systemObjectIds.Contains(e.ObjectId))
            : events;

        return ApplyNodeFilter(source);
    }

    private IEnumerable<EngineEvent> ApplyNodeFilter(IEnumerable<EngineEvent> source)
    {
        var root = FilterNodes.FirstOrDefault();

        if (root is null || root.IsChecked == true)
        {
            return source;
        }

        if (root.IsChecked == false)
        {
            return [];
        }

        var eventNodesByName = root.Children.ToDictionary(n => n.Label, StringComparer.OrdinalIgnoreCase);

        return source.Where(e =>
        {
            if (!eventNodesByName.TryGetValue(e.Name, out var eventNode))
            {
                return false;
            }

            if (eventNode.IsChecked == false)
            {
                return false;
            }

            if (eventNode.IsChecked == true || eventNode.Children.Count == 0)
            {
                return true;
            }

            return eventNode.Children.Any(c => c.IsChecked == true && c.Label == e.Description);
        });
    }

    // A single node edit can cascade to children/parents; coalesce the resulting work onto one tick.
    private void OnFilterNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!filterRefreshPending)
        {
            filterRefreshPending = true;
            dispatcherQueue.TryEnqueue(() =>
            {
                filterRefreshPending = false;
                FilterChanged?.Invoke();
            });
        }

        if (!saveUncheckedPending)
        {
            saveUncheckedPending = true;
            dispatcherQueue.TryEnqueue(async () =>
            {
                saveUncheckedPending = false;

                try
                {
                    await SaveUncheckedAsync();
                }
                catch
                {
                    // Persisting the unchecked set is best-effort; a failed save shouldn't crash the app.
                }
            });
        }
    }

    private async Task SaveUncheckedAsync()
    {
        var root = FilterNodes.FirstOrDefault();

        if (root is null)
        {
            return;
        }

        var uncheckedPaths = root.Children
            .SelectMany(eventNode => eventNode.Children
                .Where(c => c.IsChecked == false)
                .Select(c => $"{eventNode.Label}/{c.Label}"))
            .ToList();

        foreach (var eventNode in root.Children.Where(n => n is { IsChecked: false, Children.Count: 0 }))
        {
            uncheckedPaths.Add(eventNode.Label);
        }

        await settingsService.SaveSettingAsync(UncheckedSettingsKey, uncheckedPaths);
    }
}
