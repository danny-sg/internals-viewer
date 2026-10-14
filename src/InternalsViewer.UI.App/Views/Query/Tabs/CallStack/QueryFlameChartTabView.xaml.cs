using System;
using System.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryFlameChartTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string StepBackGlyph = "";

    private const string StepForwardGlyph = "";

    private const string SearchGlyph = "";

    private const string PlayGlyph = "";

    private const string PauseGlyph = "";

    private const double StackStar = 0.5;

    private const double CommandSpacing = 8;

    private ToggleButton? _instructionsToggle;

    private ToggleButton? _positionToggle;

    private ToggleButton? _searchToggle;

    private ToggleButton? _memoryToggle;

    private ToggleButton? _callStackToggle;

    private GridLength _stackHeight = new(StackStar, GridUnitType.Star);

    private Button? _playButton;

    public QueryFlameChartTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        FlameChart.CallSelected += OnCallSelected;

        FlameChart.SelectionCleared += OnSelectionCleared;

        FlameChart.CallTreeOpenRequested += OnCallTreeOpenRequested;

        FlameChart.CallStackOpenRequested += OnCallStackOpenRequested;

        FlameChart.PlanNodeSelected += OnPlanNodeSelected;

        FlameChart.PlayStateChanged += OnPlayStateChanged;

        FlameChart.PlayheadChanged += OnPlayheadChanged;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private PlayheadCallStackViewModel? Stack { get; set; }

    public FrameworkElement CreateCommands()
    {
        _positionToggle = AxisToggle("Position",
                                     TraceTimelineAxis.Position,
                                     "Width is the span of trace positions, which lines threads up against each other");

        _instructionsToggle = AxisToggle("Instructions",
                                         TraceTimelineAxis.Instructions,
                                         "Width is the instructions each call executed, so it shows how long each call took");

        _memoryToggle = TabCommands.Toggle(TabCommands.Label("Memory"),
                                           Settings.FlameChartShowMemory,
                                           "Display allocation/in-use memory",
                                           (sender, _) => Settings.FlameChartShowMemory = TabCommands.IsChecked(sender));

        _callStackToggle = TabCommands.Toggle(TabCommands.Label("Call Stack"),
                                              Settings.FlameChartShowCallStack,
                                              "Show the calls in progress at the playhead, with the values they were passed",
                                              (sender, _) => Settings.FlameChartShowCallStack = TabCommands.IsChecked(sender));

        _playButton = TabCommands.Button(TabCommands.Glyph(FlameChart.IsPlaying ? PauseGlyph : PlayGlyph),
                                         FlameChart.IsPlaying ? "Pause" : "Play",
                                         (_, _) => FlameChart.TogglePlay());

        _searchToggle = TabCommands.Toggle(TabCommands.Glyph(SearchGlyph),
                                           false,
                                           "Search function names",
                                           (sender, _) => SetSearchVisible(TabCommands.IsChecked(sender)));

        return TabCommands.Panel(CommandSpacing,
                                 _positionToggle,
                                 _instructionsToggle,
                                 TabCommands.Toggle(TabCommands.Label("Operators"),
                                                    FlameChart.ShowOperators,
                                                    "Show Plan Operators",
                                                    (sender, _) => FlameChart.ShowOperators = TabCommands.IsChecked(sender)),
                                 _memoryToggle,
                                 _callStackToggle,
                                 _playButton,
                                 TabCommands.Button(TabCommands.Glyph(StepBackGlyph), "Previous Frame", (_, _) => Step(forward: false)),
                                 TabCommands.Button(TabCommands.Glyph(StepForwardGlyph), "Next Frame", (_, _) => Step(forward: true)),
                                 _searchToggle);
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        FlameChart.CallSelected -= OnCallSelected;

        FlameChart.SelectionCleared -= OnSelectionCleared;

        FlameChart.CallTreeOpenRequested -= OnCallTreeOpenRequested;

        FlameChart.CallStackOpenRequested -= OnCallStackOpenRequested;

        FlameChart.PlanNodeSelected -= OnPlanNodeSelected;

        FlameChart.PlayStateChanged -= OnPlayStateChanged;

        FlameChart.PlayheadChanged -= OnPlayheadChanged;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        AttachStack(null);

        FlameChart.Dispose();

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

        if (Stack is { } previous && ReferenceEquals(previous.Chart, FlameChart))
        {
            previous.Chart = null;
        }

        Stack = stack;

        Stack?.Chart = FlameChart;
    }

    private ToggleButton AxisToggle(string text, TraceTimelineAxis axis, string toolTip)
    {
        var toggle = TabCommands.Toggle(TabCommands.Label(text), FlameChart.Axis == axis, toolTip, OnAxisClick);

        toggle.Tag = axis;

        return toggle;
    }

    private void OnAxisClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: TraceTimelineAxis axis })
        {
            return;
        }

        FlameChart.Axis = axis;

        _instructionsToggle?.IsChecked = axis == TraceTimelineAxis.Instructions;

        _positionToggle?.IsChecked = axis == TraceTimelineAxis.Position;
    }

    private void OnPlayheadChanged()
    {
        var isVisible = Settings.FlameChartShowCallStack;

        SetStackVisible(isVisible && FlameChart.HasCallStack);

        if (isVisible)
        {
            ViewModel?.PlayheadStack.SetStack(FlameChart.GetCallStack());
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

        FlameChart.StepPlayhead(forward);
    }

    private void OnPreviousMatchClick(object sender, RoutedEventArgs e) => FlameChart.StepSearch(forward: false);

    private void OnNextMatchClick(object sender, RoutedEventArgs e) => FlameChart.StepSearch(forward: true);

    private void SetSearchVisible(bool isVisible)
    {
        SearchRow.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        FlameChart.SearchText = isVisible ? SearchBar.Text : null;

        _searchToggle?.IsChecked = isVisible;

        if (isVisible)
        {
            SearchBar.FocusSearch();
        }
    }

    private void OnSearchTextChanged(string text) => FlameChart.SearchText = text;

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

    private void OnCallStackOpenRequested()
    {
        Stack?.ClearSelection();

        Settings.FlameChartShowCallStack = true;
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

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.FlameChartShowMemory):
                _memoryToggle?.IsChecked = Settings.FlameChartShowMemory;
                break;

            case nameof(SettingsViewModel.FlameChartShowCallStack):
                _callStackToggle?.IsChecked = Settings.FlameChartShowCallStack;

                OnPlayheadChanged();
                break;
        }
    }
}
