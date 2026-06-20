using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Replay.Events;
using InternalsViewer.UI.App.Services;

namespace InternalsViewer.UI.App.ViewModels.QueryReplay;

public partial class EventGridViewModel : ObservableObject
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

    private SettingsService SettingsService { get; }

    public EventGridViewModel(SettingsService settingsService)
    {
        SettingsService = settingsService;
    }

    private List<EngineEvent> Events { get; set; } = [];

    private HashSet<int> SystemObjectIds { get; set; } = [];

    [ObservableProperty]
    private ObservableCollection<EngineEvent> dataSource = [];

    private bool _includeSystemObjects = false;

    public bool IncludeSystemObjects
    {
        get => _includeSystemObjects;
        set
        {
            if (_includeSystemObjects == value) return;
            _includeSystemObjects = value;
            OnPropertyChanged(nameof(IncludeSystemObjects));
            RefreshDataSource();
        }
    }

    private ObservableCollection<EventFilterNode> _filterNodes = [];

    public ObservableCollection<EventFilterNode> FilterNodes
    {
        get => _filterNodes;
        private set
        {
            _filterNodes = value;
            OnPropertyChanged(nameof(FilterNodes));
        }
    }

    public void SetEvents(List<EngineEvent> events)
    {
        Events = events;
        _ = BuildFilterTreeAsync();
    }

    public void SetSystemObjectIds(HashSet<int> ids)
    {
        SystemObjectIds = ids;
        RefreshDataSource();
    }

    private async Task BuildFilterTreeAsync()
    {
        var savedUnchecked = await SettingsService.ReadSettingAsync<List<string>>(UncheckedSettingsKey);
        var uncheckedPaths = savedUnchecked is { Count: > 0 }
            ? new HashSet<string>(savedUnchecked)
            : DefaultUnchecked;

        var root = new EventFilterNode { Label = "Events" };

        var eventNodes = Events
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
                        child.PropertyChanged += OnNodeChanged;
                        return child;
                    });

                foreach (var child in children)
                {
                    eventNode.Children.Add(child);
                }

                // Sync parent state after children are set
                eventNode.RefreshCheckedState();

                eventNode.PropertyChanged += OnNodeChanged;

                return eventNode;
            });

        foreach (var node in eventNodes)
        {
            root.Children.Add(node);
        }

        root.RefreshCheckedState();
        root.PropertyChanged += OnNodeChanged;

        FilterNodes = new ObservableCollection<EventFilterNode> { root };

        RefreshDataSource();
    }

    private void OnNodeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshDataSource();
        _ = SaveUncheckedAsync();
    }

    private async Task SaveUncheckedAsync()
    {
        var root = FilterNodes.FirstOrDefault();

        if (root is null) return;

        var uncheckedPaths = root.Children
            .SelectMany(eventNode => eventNode.Children
                .Where(c => c.IsChecked == false)
                .Select(c => $"{eventNode.Label}/{c.Label}"))
            .ToList();

        // Also add fully-unchecked event-level nodes with no children
        foreach (var eventNode in root.Children.Where(n => n.IsChecked == false && n.Children.Count == 0))
        {
            uncheckedPaths.Add(eventNode.Label);
        }

        await SettingsService.SaveSettingAsync(UncheckedSettingsKey, uncheckedPaths);
    }

    private IEnumerable<EngineEvent> ApplyNodeFilter(IEnumerable<EngineEvent> source)
    {
        var root = FilterNodes.FirstOrDefault();

        if (root is null || root.IsChecked == true)
            return source;

        if (root.IsChecked == false)
            return [];

        // Root is indeterminate — filter by the event-name children
        var eventNodes = root.Children;

        return source.Where(e =>
        {
            var eventNode = eventNodes.FirstOrDefault(n => n.Label == e.Name);

            if (eventNode is null)
                return false;

            if (eventNode.IsChecked == false)
                return false;

            if (eventNode.IsChecked == true || eventNode.Children.Count == 0)
                return true;

            return eventNode.Children.Any(c => c.IsChecked == true && c.Label == e.Description);
        });
    }

    private void RefreshDataSource()
    {
        var source = !IncludeSystemObjects && SystemObjectIds.Count > 0
            ? Events.Where(e => e.ObjectId == 0 || !SystemObjectIds.Contains(e.ObjectId))
            : Events.AsEnumerable();

        DataSource = new ObservableCollection<EngineEvent>(ApplyNodeFilter(source));
    }
}
