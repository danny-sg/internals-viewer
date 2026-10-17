using System;
using System.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Trace;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryTraceTimelineTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string StepBackGlyph = "";

    private const string StepForwardGlyph = "";

    private const string SearchGlyph = "";

    private const string PlayGlyph = "";

    private const string PauseGlyph = "";

    private const double StackStar = 0.5;

    private const double CommandSpacing = 8;

    private const string ScaleGroup = "TraceTimelineScale";

    private ToggleButton? _searchToggle;

    private ToggleButton? _memoryToggle;

    private ToggleButton? _callStackToggle;

    private GridLength _stackHeight = new(StackStar, GridUnitType.Star);

    private Button? _playButton;

    private PlayheadStepMode _stepMode = PlayheadStepMode.Call;

    public QueryTraceTimelineTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        TraceTimeline.CallSelected += OnCallSelected;

        TraceTimeline.SelectionCleared += OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested += OnCallTreeOpenRequested;

        TraceTimeline.RowBufferOpenRequested += OnRowBufferOpenRequested;

        TraceTimeline.CallStackOpenRequested += OnCallStackOpenRequested;

        TraceTimeline.PlanNodeSelected += OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested += OnExecutionPlanRequested;

        TraceTimeline.PlayStateChanged += OnPlayStateChanged;

        TraceTimeline.PlayheadChanged += OnPlayheadChanged;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private PlayheadCallStackViewModel? Stack { get; set; }

    public FrameworkElement CreateCommands()
    {
        _memoryToggle = TabCommands.Toggle(TabCommands.Label("Memory"),
                                           Settings.TraceTimelineShowMemory,
                                           "Display allocation/in-use memory",
                                           (sender, _) => Settings.TraceTimelineShowMemory = TabCommands.IsChecked(sender));

        _callStackToggle = TabCommands.Toggle(TabCommands.Label("Call Stack"),
                                              Settings.TraceTimelineShowCallStack,
                                              "Show the calls in progress at the playhead, with the values they were passed",
                                              (sender, _) => Settings.TraceTimelineShowCallStack = TabCommands.IsChecked(sender));

        _callStackToggle.IsEnabled = TraceTimeline.HasCallStack;

        _playButton = TabCommands.Button(TabCommands.Glyph(TraceTimeline.IsPlaying ? PauseGlyph : PlayGlyph),
                                         TraceTimeline.IsPlaying ? "Pause" : "Play",
                                         (_, _) => TraceTimeline.TogglePlay());

        _searchToggle = TabCommands.Toggle(TabCommands.Glyph(SearchGlyph),
                                           false,
                                           "Search function names",
                                           (sender, _) => SetSearchVisible(TabCommands.IsChecked(sender)));

        return TabCommands.Panel(CommandSpacing,
                                 TabCommands.Menu("View", "Choose the chart's scale and the lanes it shows", CreateViewMenu()),
                                 _memoryToggle,
                                 _callStackToggle,
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
                                 _searchToggle);
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        TraceTimeline.CallSelected -= OnCallSelected;

        TraceTimeline.SelectionCleared -= OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested -= OnCallTreeOpenRequested;

        TraceTimeline.RowBufferOpenRequested -= OnRowBufferOpenRequested;

        TraceTimeline.CallStackOpenRequested -= OnCallStackOpenRequested;

        TraceTimeline.PlanNodeSelected -= OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested -= OnExecutionPlanRequested;

        TraceTimeline.PlayStateChanged -= OnPlayStateChanged;

        TraceTimeline.PlayheadChanged -= OnPlayheadChanged;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        AttachStack(null);

        TraceTimeline.Dispose();

        StackView.Dispose();
    }

    private void OnViewModelChanged()
    {
        Bindings.Update();

        AttachStack(ViewModel?.PlayheadStack);
    }

    private void AttachStack(PlayheadCallStackViewModel? stack)
    {
        if (ReferenceEquals(Stack, stack))
        {
            return;
        }

        if (Stack is { } previous && ReferenceEquals(previous.Chart, TraceTimeline))
        {
            previous.Chart = null;
        }

        Stack = stack;

        Stack?.Chart = TraceTimeline;
    }

    private MenuFlyout CreateViewMenu()
    {
        var scale = new MenuFlyoutSubItem { Text = "Scale" };

        scale.Items.Add(ScaleItem("Position",
                                  TraceTimelineAxis.Position,
                                  "Width is the span of trace positions, which lines threads up against each other"));

        scale.Items.Add(ScaleItem("Instructions",
                                  TraceTimelineAxis.Instructions,
                                  "Width is the instructions each call executed, so it shows how long each call took"));

        var operators = MenuItems.Toggle("Operators", TraceTimeline.ShowOperators, isChecked => TraceTimeline.ShowOperators = isChecked);

        var iteratorCalls = MenuItems.Toggle("Iterator Calls",
                                             TraceTimeline.ShowIteratorCalls,
                                             isChecked => TraceTimeline.ShowIteratorCalls = isChecked);

        var rowBuffers = MenuItems.Toggle("Row Buffers",
                                          TraceTimeline.ShowRowBuffers,
                                          isChecked => TraceTimeline.ShowRowBuffers = isChecked);

        var flameChart = MenuItems.Toggle("Flame Chart",
                                          TraceTimeline.ShowFlameChart,
                                          isChecked => TraceTimeline.ShowFlameChart = isChecked);

        var menu = new MenuFlyout();

        menu.Items.Add(scale);

        menu.Items.Add(operators);

        menu.Items.Add(iteratorCalls);

        menu.Items.Add(rowBuffers);

        menu.Items.Add(flameChart);

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(MenuItems.Toggle("Auto-Zoom", TraceTimeline.AutoZoom, isChecked => TraceTimeline.AutoZoom = isChecked));

        menu.Opening += (_, _) =>
        {
            operators.IsChecked = TraceTimeline.ShowOperators;

            iteratorCalls.IsChecked = TraceTimeline.ShowIteratorCalls;

            rowBuffers.IsChecked = TraceTimeline.ShowRowBuffers;

            flameChart.IsChecked = TraceTimeline.ShowFlameChart;
        };

        return menu;
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

    private void OnPlayheadChanged()
    {
        _callStackToggle?.IsEnabled = TraceTimeline.HasCallStack;

        var isVisible = Settings.TraceTimelineShowCallStack;

        SetStackVisible(isVisible && TraceTimeline.HasCallStack);

        if (isVisible)
        {
            ViewModel?.PlayheadStack.SetStack(TraceTimeline.GetCallStack());
        }
    }

    private void SetStackVisible(bool isVisible)
    {
        if (isVisible == (StackView.Visibility == Visibility.Visible))
        {
            return;
        }

        if (!isVisible && StackRow.Height.Value > 0)
        {
            _stackHeight = StackRow.Height;
        }

        StackView.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        StackSplitter.Visibility = StackView.Visibility;

        StackSplitterRow.Height = isVisible ? GridLength.Auto : new GridLength(0);

        StackRow.Height = isVisible ? _stackHeight : new GridLength(0);
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

    private void Step(bool forward)
    {
        Stack?.ClearSelection();

        TraceTimeline.StepPlayhead(forward, _stepMode);
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

    private void OnSearchTextChanged(string text) => TraceTimeline.SearchText = text;

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        SetSearchVisible(false);

        e.Handled = true;
    }

    private void OnCallSelected(CallStackNode node, int call) => ViewModel?.NavigateToCall(node, call);

    private void OnCallTreeOpenRequested(CallStackNode? node, int call) => ViewModel?.OpenCallTree(node, call);

    private void OnRowBufferOpenRequested(CallStackNode node, int call, bool isBatch) => ViewModel?.OpenRowBuffers(node, call, isBatch);

    private void OnCallStackOpenRequested()
    {
        Stack?.ClearSelection();

        Settings.TraceTimelineShowCallStack = true;
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

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.TraceTimelineShowMemory):
                _memoryToggle?.IsChecked = Settings.TraceTimelineShowMemory;
                break;

            case nameof(SettingsViewModel.TraceTimelineShowCallStack):
                _callStackToggle?.IsChecked = Settings.TraceTimelineShowCallStack;

                OnPlayheadChanged();
                break;
        }
    }
}
