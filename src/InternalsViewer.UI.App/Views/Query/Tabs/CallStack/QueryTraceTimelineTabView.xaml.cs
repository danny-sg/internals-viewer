using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Dialogs;
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
using Windows.UI;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryTraceTimelineTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string StepBackGlyph = "";

    private const string StepForwardGlyph = "";

    private const string SearchGlyph = "";

    private const string PlayGlyph = "";

    private const string PauseGlyph = "";

    private const string FilterGlyph = "";

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


        TraceTimeline.CallStackOpenRequested += OnCallStackOpenRequested;

        TraceTimeline.PlanNodeSelected += OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested += OnExecutionPlanRequested;

        TraceTimeline.TickTrackVisibilityChanged += OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested += OnTickTrackAddRequested;

        TraceTimeline.TickTracksClearRequested += OnTickTracksClearRequested;

        TraceTimeline.PlayStateChanged += OnPlayStateChanged;

        TraceTimeline.CallStackChanged += OnCallStackChanged;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        SearchBar.StepRequested += OnSearchStepRequested;

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
                                              "Show the calls that led to the selected call, with the values they were passed",
                                              (sender, _) => Settings.TraceTimelineShowCallStack = TabCommands.IsChecked(sender));

        _playButton = TabCommands.Button(TabCommands.Glyph(TraceTimeline.IsPlaying ? PauseGlyph : PlayGlyph),
                                         TraceTimeline.IsPlaying ? "Pause" : "Play",
                                         (_, _) => TraceTimeline.TogglePlay());

        _searchToggle = TabCommands.Toggle(TabCommands.Glyph(SearchGlyph),
                                           false,
                                           "Search function names",
                                           (sender, _) => SetSearchVisible(TabCommands.IsChecked(sender)));

        var categoryToggle = TabCommands.Toggle(TabCommands.Glyph(FilterGlyph),
                                                CategoryFilter.IsPanelVisible,
                                                "Filter by Category",
                                                (sender, _) => CategoryFilter.SetPanelVisible(TabCommands.IsChecked(sender)));

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
                                 _searchToggle,
                                 categoryToggle);
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        TraceTimeline.CallSelected -= OnCallSelected;

        TraceTimeline.SelectionCleared -= OnSelectionCleared;

        TraceTimeline.CallTreeOpenRequested -= OnCallTreeOpenRequested;


        TraceTimeline.CallStackOpenRequested -= OnCallStackOpenRequested;

        TraceTimeline.PlanNodeSelected -= OnPlanNodeSelected;

        TraceTimeline.ExecutionPlanRequested -= OnExecutionPlanRequested;

        TraceTimeline.TickTrackVisibilityChanged -= OnTickTrackVisibilityChanged;

        TraceTimeline.TickTrackAddRequested -= OnTickTrackAddRequested;

        TraceTimeline.TickTracksClearRequested -= OnTickTracksClearRequested;

        TraceTimeline.PlayStateChanged -= OnPlayStateChanged;

        TraceTimeline.CallStackChanged -= OnCallStackChanged;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        SearchBar.StepRequested -= OnSearchStepRequested;

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

        var flameChart = MenuItems.Toggle("Flame Chart",
                                          TraceTimeline.ShowFlameChart,
                                          isChecked => TraceTimeline.ShowFlameChart = isChecked);

        var highlightRegions = MenuItems.Toggle("Highlight Regions",
                                                TraceTimeline.ShowHighlightRegions,
                                                isChecked => TraceTimeline.ShowHighlightRegions = isChecked);

        var menu = new MenuFlyout();

        menu.Items.Add(scale);

        menu.Items.Add(operators);

        List<MenuFlyoutItemBase> tracks = [];

        menu.Items.Add(flameChart);

        menu.Items.Add(highlightRegions);

        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(MenuItems.Toggle("Auto-Zoom", TraceTimeline.AutoZoom, isChecked => TraceTimeline.AutoZoom = isChecked));

        menu.Opening += (_, _) =>
        {
            operators.IsChecked = TraceTimeline.ShowOperators;

            foreach (var item in tracks)
            {
                menu.Items.Remove(item);
            }

            tracks = [.. TraceTimeline.CreateTickTrackItems()];

            var index = menu.Items.IndexOf(operators) + 1;

            foreach (var item in tracks)
            {
                menu.Items.Insert(index++, item);
            }

            flameChart.IsChecked = TraceTimeline.ShowFlameChart;

            highlightRegions.IsChecked = TraceTimeline.ShowHighlightRegions;
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

    private void OnCallStackChanged()
    {
        var isVisible = Settings.TraceTimelineShowCallStack && TraceTimeline.HasCallStack;

        SetStackVisible(isVisible);

        if (isVisible)
        {
            ViewModel?.PlayheadStack.SetStack(TraceTimeline.GetCallStack(), TraceTimeline.GetSelectedCall());
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

    private void OnCallSelected(CallStackNode node, int call) => ViewModel?.NavigateToCall(node, call);

    private void OnCallTreeOpenRequested(CallStackNode? node, int call) => ViewModel?.OpenCallTree(node, call);


    private void OnCallStackOpenRequested() => Settings.TraceTimelineShowCallStack = true;

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

    private void OnTickTracksClearRequested() => ViewModel?.ClearTickTracks();

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

                OnCallStackChanged();
                break;
        }
    }
}
