using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Query.Trace.Timeline;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.Views.Query.Trace.OperatorTracks;
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

    private const int MemoryRefreshMilliseconds = 50;

    private const int StackRefreshMilliseconds = 100;

    private ToggleButton? _searchToggle;

    private ToggleButton? _operatorsToggle;

    private ToggleButton? _memoryHistoryToggle;

    private ToggleButton? _callsToggle;

    private Button? _playButton;

    private PlayheadStepMode _stepMode = PlayheadStepMode.Call;

    public QueryTraceTimelineTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        TraceTimeline.CallSelected += OnCallSelected;

        TraceTimeline.SelectionCleared += OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested += OnCallTreeOpenRequested;

        TraceTimeline.SearchRequested += Search;

        TraceTimeline.CallStackVisibilityRequested += OnCallStackVisibilityRequested;

        TraceTimeline.PaneVisibilityChanged += OnPaneVisibilityChanged;

        TraceTimeline.PlanNodeSelected += OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested += OnExecutionPlanRequested;

        TraceTimeline.MemoryWriteSelected += OnMemoryWriteSelected;

        TraceTimeline.MemoryBlockSelected += OnMemoryBlockSelected;

        TraceTimeline.TickTrackVisibilityChanged += OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested += OnTickTrackAddRequested;

        TraceTimeline.PlayStateChanged += OnPlayStateChanged;

        TraceTimeline.CallStackChanged += OnCallStackChanged;

        TraceTimeline.PlayheadChanged += OnPlayheadChanged;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        SearchBar.StepRequested += OnSearchStepRequested;

        AddHandler(PointerPressedEvent, NavigationPressedHandler, true);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private PointerEventHandler NavigationPressedHandler => field ??= OnNavigationPointerPressed;

    private TraceTimelineTabViewModel? Tab { get; set; }

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

        _searchToggle = TabCommands.Toggle(TabCommands.Glyph(SearchGlyph),
                                           false,
                                           "Search function names",
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
                                 TabCommands.ToggleSplitButton("Choose what Previous Frame and Next Frame step to",
                                                               "Step",
                                                               (int)_stepMode,
                                                               index => _stepMode = (PlayheadStepMode)index,
                                                               "Call",
                                                               "Same Call",
                                                               "Batch",
                                                               "Row"),
                                 TabCommands.Button(TabCommands.Glyph(StepBackGlyph), "Previous Frame", (_, _) => Step(forward: false)),
                                 TabCommands.Button(TabCommands.Glyph(StepForwardGlyph), "Next Frame", (_, _) => Step(forward: true)),
                                 TabCommands.Separator(),
                                 _searchToggle,
                                 categoryToggle);
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        MemoryThrottle.Dispose();

        StackThrottle.Dispose();

        TraceTimeline.CallSelected -= OnCallSelected;

        TraceTimeline.SelectionCleared -= OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested -= OnCallTreeOpenRequested;

        TraceTimeline.SearchRequested -= Search;

        TraceTimeline.CallStackVisibilityRequested -= OnCallStackVisibilityRequested;

        TraceTimeline.PaneVisibilityChanged -= OnPaneVisibilityChanged;

        TraceTimeline.PlanNodeSelected -= OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested -= OnExecutionPlanRequested;

        TraceTimeline.MemoryWriteSelected -= OnMemoryWriteSelected;

        TraceTimeline.MemoryBlockSelected -= OnMemoryBlockSelected;

        TraceTimeline.TickTrackVisibilityChanged -= OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested -= OnTickTrackAddRequested;

        TraceTimeline.PlayStateChanged -= OnPlayStateChanged;

        TraceTimeline.CallStackChanged -= OnCallStackChanged;

        TraceTimeline.PlayheadChanged -= OnPlayheadChanged;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        SearchBar.StepRequested -= OnSearchStepRequested;

        RemoveHandler(PointerPressedEvent, NavigationPressedHandler);

        AttachTab(null);

        TraceTimeline.Dispose();
    }

    private void OnViewModelChanged()
    {
        Bindings.Update();

        AttachTab(ViewModel?.TraceTimelineTab);
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

        menu.Items.Add(MenuItems.SubMenu("Memory History", sizeToFit, highlightHover, latching, buffers));

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

    private void RefreshMemory()
    {
        if (TraceTimeline.GetPlayheadPosition() is { } position)
        {
            Tab?.SetPlayhead(position);
        }
    }

    private void OnPlayheadRequested(object? sender, TracePoint point) => TraceTimeline.MovePlayheadTo(point);

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

    private void Step(bool forward) => TraceTimeline.StepPlayhead(forward, _stepMode);

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

    private void OnSearchTextChanged(string text) => TraceTimeline.SearchText = text;

    private void OnSearchStepRequested(bool forward) => TraceTimeline.StepSearch(forward);

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

    private async void OnTickTrackAddRequested(CallStackNode? node)
    {
        if (ViewModel is { } viewModel && await OperatorTrackEditor.ShowAsync(XamlRoot, viewModel, null, node) is { } definition)
        {
            viewModel.AddTickTrack(definition);
        }
    }

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
}
