using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.Views.Query.Trace.OperatorTracks;
using InternalsViewer.UI.Plugins.FullTrace.Menus;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline;

public sealed partial class QueryTraceTimelineTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string StepBackGlyph = "";

    private const string StepForwardGlyph = "";

    private const string SearchGlyph = "";

    private const string PlayGlyph = "";

    private const string PauseGlyph = "";

    private const double CommandSpacing = 8;

    private const string ScaleGroup = "TraceTimelineScale";

    private const string MemoryGroup = "TraceTimelineViewMemory";

    private const string StepGroup = "TraceTimelineStep";

    private const string CallStepText = "Call";

    private const string SameCallStepText = "Same Call";

    private const int MemoryRefreshMilliseconds = 50;

    private const int StackRefreshMilliseconds = 100;

    private ToggleButton? _searchToggle;

    private ToggleButton? _operatorsToggle;

    private ToggleButton? _memoryHistoryToggle;

    private ToggleButton? _callsToggle;

    private Button? _playButton;

    private Button? _previousFrameButton;

    private Button? _nextFrameButton;

    private TextBlock? _stepLabel;

    private long _rulerPinnedToken;

    public QueryTraceTimelineTabView()
    {
        InitializeComponent();

        SearchAccelerator.ScopeOwner = this;

        DataContextChanged += (_, _) => OnViewModelChanged();

        _rulerPinnedToken = TraceTimeline.RegisterPropertyChangedCallback(TraceTimelineControl.IsRulerPinnedProperty, OnRulerPinnedChanged);

        TraceTimeline.CallSelected += OnCallSelected;

        TraceTimeline.SelectionCleared += OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested += OnCallTreeOpenRequested;

        TraceTimeline.CallMenuRequested += OnCallMenuRequested;

        TraceTimeline.SearchRequested += Search;

        TraceTimeline.CallStackVisibilityRequested += OnCallStackVisibilityRequested;

        TraceTimeline.PaneVisibilityChanged += OnPaneVisibilityChanged;

        TraceTimeline.PlanNodeSelected += OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested += OnExecutionPlanRequested;

        TraceTimeline.OperatorMenuRequested += OnOperatorMenuRequested;

        TraceTimeline.MemoryWriteSelected += OnMemoryWriteSelected;

        TraceTimeline.MemoryBlockSelected += OnMemoryBlockSelected;

        TraceTimeline.TickTrackVisibilityChanged += OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested += OnTickTrackAddRequested;

        TraceTimeline.PlayStateChanged += OnPlayStateChanged;

        TraceTimeline.StepModeChanged += OnStepModeChanged;

        TraceTimeline.CallStackChanged += OnCallStackChanged;

        TraceTimeline.PlayheadChanged += OnPlayheadChanged;

        TraceTimeline.TrimChanged += OnTrimChanged;

        TraceTimeline.ReferenceGraphRequested += OnReferenceGraphRequested;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        SearchBar.StepRequested += OnSearchStepRequested;

        SearchBar.SuggestionChosen += OnSearchSuggestionChosen;

        AddHandler(PointerPressedEvent, NavigationPressedHandler, true);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private PointerEventHandler NavigationPressedHandler => field ??= OnNavigationPointerPressed;

    private TraceTimelineTabViewModel? Tab { get; set; }

    private QueryViewModel? RulerOwner { get; set; }

    private UiThrottle MemoryThrottle { get; }
        = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), TimeSpan.FromMilliseconds(MemoryRefreshMilliseconds));

    private UiThrottle StackThrottle { get; }
        = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), TimeSpan.FromMilliseconds(StackRefreshMilliseconds));

    public FrameworkElement CreateCommands()
    {
        _operatorsToggle = TabCommands.Toggle(TabCommands.Label("Operators"),
                                              TraceTimeline.ShowOperators,
                                              "Show the Operators pane, a row per plan operator",
                                              (sender, _) => SetPaneVisible(sender, isVisible => TraceTimeline.ShowOperators = isVisible));

        _memoryHistoryToggle = TabCommands.Toggle(TabCommands.Label("Memory"),
                                                  TraceTimeline.ShowMemoryHistory,
                                                  "Show the Memory History lane, the memory blocks the query wrote to",
                                                  (sender, _) => SetPaneVisible(sender, isVisible => TraceTimeline.ShowMemoryHistory = isVisible));

        _callsToggle = TabCommands.Toggle(TabCommands.Label("Calls"),
                                          TraceTimeline.ShowFlameChart,
                                          "Show the thread lanes, a bar for every call",
                                          (sender, _) => SetPaneVisible(sender, isVisible => TraceTimeline.ShowFlameChart = isVisible));

        foreach (var toggle in PaneToggles)
        {
            toggle.SizeChanged += OnPaneToggleSizeChanged;
        }

        _playButton = TabCommands.Button(TabCommands.Glyph(TraceTimeline.IsPlaying ? PauseGlyph : PlayGlyph),
                                         TraceTimeline.IsPlaying ? "Pause" : "Play",
                                         (_, _) => TraceTimeline.TogglePlay());

        _previousFrameButton = TabCommands.Button(TabCommands.Glyph(StepBackGlyph), "Previous Frame", (_, _) => Step(forward: false));

        _nextFrameButton = TabCommands.Button(TabCommands.Glyph(StepForwardGlyph), "Next Frame", (_, _) => Step(forward: true));

        UpdatePlaybackVisibility();

        _searchToggle = TabCommands.Toggle(TabCommands.Glyph(SearchGlyph),
                                           false,
                                           "Search Calls and Memory",
                                           (sender, _) => SetSearchVisible(TabCommands.IsChecked(sender)));

        var categoryToggle = TabCommands.Toggle(TabCommands.FilterGlyph(),
                                                CategoryFilter.IsPanelVisible,
                                                "Filter by Category",
                                                (sender, _) => CategoryFilter.SetPanelVisible(TabCommands.IsChecked(sender)));

        return TabCommands.Panel(CommandSpacing,
                                 TabCommands.Menu("View", "Choose the chart's scale and what each part of it shows", CreateViewMenu()),
                                 _operatorsToggle,
                                 _memoryHistoryToggle,
                                 _callsToggle,
                                 _playButton,
                                 TabCommands.Separator(),
                                 CreateStepMenu(),
                                 _previousFrameButton,
                                 _nextFrameButton,
                                 TabCommands.Separator(),
                                 _searchToggle,
                                 categoryToggle);
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        MemoryThrottle.Dispose();

        StackThrottle.Dispose();

        TraceTimeline.UnregisterPropertyChangedCallback(TraceTimelineControl.IsRulerPinnedProperty, _rulerPinnedToken);

        TraceTimeline.CallSelected -= OnCallSelected;

        TraceTimeline.SelectionCleared -= OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested -= OnCallTreeOpenRequested;

        TraceTimeline.CallMenuRequested -= OnCallMenuRequested;

        TraceTimeline.SearchRequested -= Search;

        TraceTimeline.CallStackVisibilityRequested -= OnCallStackVisibilityRequested;

        TraceTimeline.PaneVisibilityChanged -= OnPaneVisibilityChanged;

        TraceTimeline.PlanNodeSelected -= OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested -= OnExecutionPlanRequested;

        TraceTimeline.OperatorMenuRequested -= OnOperatorMenuRequested;

        TraceTimeline.MemoryWriteSelected -= OnMemoryWriteSelected;

        TraceTimeline.MemoryBlockSelected -= OnMemoryBlockSelected;

        TraceTimeline.TickTrackVisibilityChanged -= OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested -= OnTickTrackAddRequested;

        TraceTimeline.PlayStateChanged -= OnPlayStateChanged;

        TraceTimeline.StepModeChanged -= OnStepModeChanged;

        TraceTimeline.CallStackChanged -= OnCallStackChanged;

        TraceTimeline.PlayheadChanged -= OnPlayheadChanged;

        TraceTimeline.TrimChanged -= OnTrimChanged;

        TraceTimeline.ReferenceGraphRequested -= OnReferenceGraphRequested;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        SearchBar.StepRequested -= OnSearchStepRequested;

        SearchBar.SuggestionChosen -= OnSearchSuggestionChosen;

        RemoveHandler(PointerPressedEvent, NavigationPressedHandler);

        AttachTab(null);

        AttachRuler(null);

        Ruler.Dispose();

        TraceTimeline.Dispose();
    }

    private void OnViewModelChanged()
    {
        Bindings.Update();

        AttachTab(ViewModel?.TraceTimelineTab);

        AttachRuler(ViewModel);
    }

    private void AttachRuler(QueryViewModel? owner)
    {
        if (RulerOwner is { } previous && ReferenceEquals(previous.TraceRuler, TraceTimeline))
        {
            previous.TraceRuler = null;
        }

        RulerOwner = owner;

        owner?.TraceRuler = TraceTimeline;
    }

    private void AttachTab(TraceTimelineTabViewModel? tab)
    {
        if (ReferenceEquals(Tab, tab))
        {
            return;
        }

        if (Tab is { } previous)
        {
            previous.PropertyChanged -= OnTabPropertyChanged;

            previous.PlayheadRequested -= OnPlayheadRequested;

            previous.MemoryHighlightRequested -= OnMemoryHighlightRequested;

            if (ReferenceEquals(previous.Chart, TraceTimeline))
            {
                previous.Chart = null;
            }

            if (ReferenceEquals(previous.Controller, this))
            {
                previous.Controller = null;
            }
        }

        Tab = tab;

        if (tab is null)
        {
            return;
        }

        tab.PropertyChanged += OnTabPropertyChanged;

        tab.PlayheadRequested += OnPlayheadRequested;

        tab.MemoryHighlightRequested += OnMemoryHighlightRequested;

        tab.Chart = TraceTimeline;

        tab.Controller = this;

        tab.SetPlayhead(TraceTimeline.GetPlayheadPosition());

        OnCallStackChanged();
    }

    private MenuFlyout CreateViewMenu()
    {
        var position = ScaleItem("Position",
                                 TraceTimelineAxis.Position,
                                 "Width is the span of trace positions, which lines threads up against each other");

        var instructions = ScaleItem("Instructions",
                                     TraceTimelineAxis.Instructions,
                                     "Width is the instructions each call executed, so it shows how long each call took");

        var operators = new MenuFlyoutSubItem { Text = "Operators" };

        var writes = MenuItems.Toggle("Writes", TraceTimeline.ShowMemoryWrites, isChecked => TraceTimeline.ShowMemoryWrites = isChecked);

        var reads = MenuItems.Toggle("Reads", TraceTimeline.ShowMemoryReads, isChecked => TraceTimeline.ShowMemoryReads = isChecked);

        var sizeToFit = MenuItems.Toggle("Size to Fit",
                                         TraceTimeline.MemorySizeToFit,
                                         isChecked => TraceTimeline.MemorySizeToFit = isChecked);

        var highlightHover = MenuItems.Toggle("Call Highlight Hover",
                                              TraceTimeline.HighlightMemoryWriters,
                                              isChecked => TraceTimeline.HighlightMemoryWriters = isChecked);

        var latching = MenuItems.Toggle("Latching",
                                        TraceTimeline.ShowBufferLatches,
                                        isChecked => TraceTimeline.ShowBufferLatches = isChecked);

        var buffers = MenuItems.Toggle("BUF", TraceTimeline.ShowBuffers, isChecked => TraceTimeline.ShowBuffers = isChecked);

        RadioMenuFlyoutItem[] memoryItems =
        [
            MemoryItem("None", null),
            MemoryItem("In Use", TraceTimelineMemoryMode.InUse),
            MemoryItem("Allocated", TraceTimelineMemoryMode.Allocated)
        ];

        ToggleMenuFlyoutItem[] highlightItems =
        [
            .. TraceTimelineControl.HighlightRegionLabels.Select(label => MenuItems.Toggle(label,
                                                                                            TraceTimeline.IsHighlightRegionVisible(label),
                                                                                            isChecked => TraceTimeline.SetHighlightRegionVisible(label, isChecked)))
        ];

        var callStack = MenuItems.Toggle("Call Stack", Tab?.IsCallStackVisible == true, isChecked => Tab?.IsCallStackVisible = isChecked);

        var autoZoom = MenuItems.Toggle("Auto-Zoom", TraceTimeline.AutoZoom, isChecked => TraceTimeline.AutoZoom = isChecked);

        var menu = new MenuFlyout();

        menu.Items.Add(MenuItems.SubMenu("Scale", position, instructions));

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(operators);

        menu.Items.Add(MenuItems.SubMenu("Memory History", writes, reads, new MenuFlyoutSeparator(), sizeToFit, highlightHover, latching, buffers));

        menu.Items.Add(MenuItems.SubMenu("Flame Chart",
                                         MenuItems.SubMenu("Memory", memoryItems),
                                         MenuItems.SubMenu("Highlight Regions", highlightItems)));

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(callStack);

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(autoZoom);

        menu.Items.Add(MenuItems.Create("Reset Layout", ResetLayout));

        menu.Opening += (_, _) =>
        {
            position.IsChecked = TraceTimeline.Axis == TraceTimelineAxis.Position;

            instructions.IsChecked = TraceTimeline.Axis == TraceTimelineAxis.Instructions;

            operators.Items.Clear();

            foreach (var item in TraceTimeline.CreateTickTrackItems())
            {
                operators.Items.Add(item);
            }

            writes.IsChecked = TraceTimeline.ShowMemoryWrites;

            reads.IsChecked = TraceTimeline.ShowMemoryReads;

            sizeToFit.IsChecked = TraceTimeline.MemorySizeToFit;

            highlightHover.IsChecked = TraceTimeline.HighlightMemoryWriters;

            latching.IsChecked = TraceTimeline.ShowBufferLatches;

            buffers.IsChecked = TraceTimeline.ShowBuffers;

            foreach (var item in memoryItems)
            {
                item.IsChecked = item.Tag is TraceTimelineMemoryMode mode
                    ? TraceTimeline.ShowMemory && TraceTimeline.MemoryMode == mode
                    : !TraceTimeline.ShowMemory;
            }

            foreach (var item in highlightItems)
            {
                item.IsChecked = TraceTimeline.IsHighlightRegionVisible(item.Text);
            }

            callStack.IsChecked = Tab?.IsCallStackVisible == true;

            autoZoom.IsChecked = TraceTimeline.AutoZoom;
        };

        return menu;
    }

    private Button CreateStepMenu()
    {
        _stepLabel = TabCommands.Label(GetStepLabel());

        var menu = new MenuFlyout();

        menu.Opening += (_, _) => FillStepMenu(menu);

        return TabCommands.Menu(_stepLabel, "Choose what Previous Frame and Next Frame step to", menu);
    }

    private void FillStepMenu(MenuFlyout menu)
    {
        menu.Items.Clear();

        menu.Items.Add(StepItem(CallStepText, PlayheadStepMode.Call, null));

        menu.Items.Add(StepItem(SameCallStepText, PlayheadStepMode.SameCall, null));

        var tracks = TraceTimeline.GetStepTracks().ToList();

        if (tracks.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var track in tracks)
        {
            menu.Items.Add(StepItem(track.MenuText, PlayheadStepMode.Track, track.Name));
        }
    }

    private RadioMenuFlyoutItem StepItem(string text, PlayheadStepMode mode, string? track)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = StepGroup,
            IsChecked = TraceTimeline.StepMode == mode && string.Equals(TraceTimeline.StepTrack, track, StringComparison.Ordinal)
        };

        item.Click += (_, _) => TraceTimeline.SetStepMode(mode, track);

        return item;
    }

    private string GetStepLabel()
    {
        var text = TraceTimeline.StepMode switch
        {
            PlayheadStepMode.SameCall => SameCallStepText,
            PlayheadStepMode.Track => TraceTimeline.GetStepTrack(TraceTimeline.StepTrack)?.MenuText ?? CallStepText,
            _ => CallStepText
        };

        return $"Step {text}";
    }

    private void OnStepModeChanged() => _stepLabel?.Text = GetStepLabel();

    private RadioMenuFlyoutItem MemoryItem(string text, TraceTimelineMemoryMode? mode)
    {
        var item = new RadioMenuFlyoutItem { Text = text, GroupName = MemoryGroup, Tag = mode };

        item.Click += (_, _) => TraceTimeline.SetMemory(mode);

        return item;
    }

    private void SetPaneVisible(object sender, Action<bool> setVisible)
    {
        var isVisible = TabCommands.IsChecked(sender);

        if (!isVisible && VisiblePaneCount == 0)
        {
            ((ToggleButton)sender).IsChecked = true;

            return;
        }

        setVisible(isVisible);
    }

    private int VisiblePaneCount => PaneToggles.Count(t => t.IsChecked == true);

    private IEnumerable<ToggleButton> PaneToggles
        => new[] { _operatorsToggle, _memoryHistoryToggle, _callsToggle }.OfType<ToggleButton>();

    private void OnPaneToggleSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = PaneToggles.Max(t => t.ActualWidth);

        foreach (var toggle in PaneToggles)
        {
            if (toggle.MinWidth < width)
            {
                toggle.MinWidth = width;
            }
        }
    }

    private void ResetLayout()
    {
        Tab?.ResetLayout();

        TraceTimeline.ResetLayout();
    }

    private RadioMenuFlyoutItem ScaleItem(string text, TraceTimelineAxis axis, string toolTip)
    {
        var item = new RadioMenuFlyoutItem { Text = text, GroupName = ScaleGroup, Tag = axis, IsChecked = TraceTimeline.Axis == axis };

        ToolTipService.SetToolTip(item, toolTip);

        item.Click += OnScaleClick;

        return item;
    }

    private void OnScaleClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: TraceTimelineAxis axis })
        {
            TraceTimeline.Axis = axis;
        }
    }

    private void OnCallStackChanged()
    {
        if (Tab is { IsCallStackVisible: true })
        {
            StackThrottle.Run(RefreshStack);
        }

        OnPlayheadChanged();
    }

    private void RefreshStack()
    {
        if (Tab is { IsCallStackVisible: true } tab)
        {
            tab.PlayheadStack.SetStack(TraceTimeline.GetCallStack(), TraceTimeline.GetSelectedCall());
        }
    }

    private void OnPlayheadChanged() => MemoryThrottle.Run(RefreshMemory);

    private void OnTrimChanged(TracePosition? start, TracePosition? end) => Tab?.SetTrim(start, end);

    private void OnReferenceGraphRequested(int block, TracePosition position) => Tab?.OpenReferenceGraph(block, position);

    private void RefreshMemory()
    {
        if (TraceTimeline.GetPlayheadPosition() is { } position)
        {
            Tab?.SetPlayhead(position);
        }
    }

    private void OnPlayheadRequested(object? sender, TracePoint point) => TraceTimeline.MovePlayheadTo(point);

    private void OnMemoryHighlightRequested(object? sender, int? block) => TraceTimeline.HighlightMemoryBlock(block);

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraceTimelineTabViewModel.IsCallStackVisible))
        {
            OnCallStackChanged();
        }
    }

    private void OnPlayStateChanged(bool isPlaying)
    {
        if (_playButton is not { Content: FontIcon icon } button)
        {
            return;
        }

        icon.Glyph = isPlaying ? PauseGlyph : PlayGlyph;

        ToolTipService.SetToolTip(button, isPlaying ? "Pause" : "Play");
    }

    private void Step(bool forward) => TraceTimeline.Step(forward);

    private void OnRulerPinnedChanged(DependencyObject sender, DependencyProperty property) => UpdatePlaybackVisibility();

    private void UpdatePlaybackVisibility()
    {
        var visibility = TraceTimeline.IsRulerPinned ? Visibility.Collapsed : Visibility.Visible;

        _playButton?.Visibility = visibility;

        _previousFrameButton?.Visibility = visibility;

        _nextFrameButton?.Visibility = visibility;
    }

    private void OnPreviousMatchClick(object sender, RoutedEventArgs e) => TraceTimeline.StepSearch(forward: false);

    private void OnNextMatchClick(object sender, RoutedEventArgs e) => TraceTimeline.StepSearch(forward: true);

    private void SetSearchVisible(bool isVisible)
    {
        SearchRow.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        TraceTimeline.SearchText = isVisible ? SearchBar.Text : null;

        _searchToggle?.IsChecked = isVisible;

        if (isVisible)
        {
            SearchBar.FocusSearch();
        }
    }

    private void OnSearchTextChanged(string text)
    {
        TraceTimeline.SearchText = text;

        SearchBar.Suggestions = TraceTimeline.GetSearchSuggestions();
    }

    private void OnCallSearchClick(object sender, RoutedEventArgs e)
    {
        TraceTimeline.IsCallSearch = TabCommands.IsChecked(sender);

        SearchBar.Suggestions = TraceTimeline.GetSearchSuggestions();
    }

    private void OnMemorySearchClick(object sender, RoutedEventArgs e)
    {
        TraceTimeline.IsMemorySearch = TabCommands.IsChecked(sender);

        SearchBar.Suggestions = TraceTimeline.GetSearchSuggestions();
    }

    private void OnSearchSuggestionChosen(object suggestion)
    {
        SearchBar.Text = suggestion switch
        {
            CallSearchSuggestion call => call.Name,
            MemorySearchSuggestion memory => memory.Label,
            _ => SearchBar.Text
        };
    }

    private void OnOpenMemorySuggestionClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MemorySearchSuggestion memory)
        {
            SearchBar.CloseSuggestions();

            Tab?.OpenMemory(memory.Block, memory.Address, memory.Position);
        }
    }

    private void OnSearchStepRequested(bool forward) => TraceTimeline.StepSearch(forward);

    private void OnSearchAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SetSearchVisible(true);

        args.Handled = true;
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        SetSearchVisible(false);

        e.Handled = true;
    }

    private void OnNavigationPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsXButton1Pressed)
        {
            TraceTimeline.GoBack();

            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            TraceTimeline.GoForward();

            e.Handled = true;
        }
    }

    private void OnCallSelected(CallStackNode node, int callIndex) => ViewModel?.NavigateToCall(node, callIndex);

    private void OnCallTreeOpenRequested(CallStackNode? node, int callIndex) => ViewModel?.OpenCallTree(node, callIndex);

    private void OnCallMenuRequested(MenuFlyout menu, TraceMenuTarget target)
    {
        if (Tab is not { } tab)
        {
            return;
        }

        foreach (var item in tab.GetMenuItems(target))
        {
            menu.Items.Add(MenuItems.Create(item.Text, item.Invoke));
        }
    }

    private void OnCallStackVisibilityRequested(bool isVisible) => ViewModel?.TraceTimelineTab.IsCallStackVisible = isVisible;

    private void OnPaneVisibilityChanged()
    {
        _operatorsToggle?.IsChecked = TraceTimeline.ShowOperators;

        _memoryHistoryToggle?.IsChecked = TraceTimeline.ShowMemoryHistory;

        _callsToggle?.IsChecked = TraceTimeline.ShowFlameChart;
    }

    private void OnTickTrackVisibilityChanged(string name, bool isVisible)
    {
        var hidden = new HashSet<string>(Settings.TraceTimelineHiddenTickTrackSet, StringComparer.Ordinal);

        if (isVisible)
        {
            hidden.Remove(name);
        }
        else
        {
            hidden.Add(name);
        }

        Settings.TraceTimelineHiddenTickTracks = string.Join(SettingsViewModel.CategorySeparator, hidden.Order(StringComparer.Ordinal));
    }

#pragma warning disable VSTHRD100
    private async void OnTickTrackAddRequested(CallStackNode? node)
    {
        if (ViewModel is { } viewModel && await OperatorTrackEditor.ShowAsync(XamlRoot, viewModel, null, node) is { } definition)
        {
            viewModel.AddTickTrack(definition);
        }
    }
#pragma warning restore VSTHRD100

    private void OnSelectionCleared()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.Arguments.SelectedCall = null;

        viewModel.SelectedCallNode = null;
    }

    private void OnPlanNodeSelected(PlanNodeIdentifier identifier) => ViewModel?.SelectPlanNode(identifier);

    private void OnMemoryWriteSelected(int block, TracePosition? position) => ViewModel?.ShowMemoryWrite(block, position);

    private void OnMemoryBlockSelected(int block, ulong address, TracePosition position)
        => ViewModel?.TraceTimelineTab.OpenMemory(block, address, position);

    private void OnExecutionPlanRequested(PlanNodeIdentifier? identifier)
    {
        if (identifier is not null)
        {
            ViewModel?.OpenExecutionPlan(identifier);
        }
        else
        {
            ViewModel?.Layout.ShowExecutionPlan();
        }
    }

    private void OnOperatorMenuRequested(MenuFlyout menu, ExecutionOperatorEvent op)
    {
        if (ViewModel is { } viewModel && viewModel.GetIndexName(op) is { } index)
        {
            menu.Items.Add(MenuItems.Create($"Open Index: {index}", () => viewModel.OpenIndex(op)));
        }
    }
}
