using System;
using System.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace InternalsViewer.UI.App.Views.Query.Tabs.FlameChart;

public sealed partial class QueryFlameChartTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string LockedGlyph = "\uE72E";

    private const string UnlockedGlyph = "\uE785";

    private const string StepBackGlyph = "\uE892";

    private const string StepForwardGlyph = "\uE893";

    private const string SearchGlyph = "\uE721";

    private const string PlayGlyph = "\uE768";

    private const string PauseGlyph = "\uE769";

    private ToggleButton? _instructionsToggle;

    private ToggleButton? _positionToggle;

    private ToggleButton? _searchToggle;

    private ToggleButton? _lockToggle;

    private ToggleButton? _memoryToggle;

    private Button? _playButton;

    public QueryFlameChartTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        FlameChart.CallSelected += OnCallSelected;

        FlameChart.SelectionCleared += OnSelectionCleared;

        FlameChart.CallStackOpenRequested += OnCallStackOpenRequested;

        FlameChart.PlanNodeSelected += OnPlanNodeSelected;

        FlameChart.PlayStateChanged += OnPlayStateChanged;

        FlameChart.LockChanged += OnLockChanged;

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        FlameChart.HiddenCategories = SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories);

        FlameChart.PrunedCategories = SettingsViewModel.SplitCategories(Settings.CallTreePrunedCategories);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    public FrameworkElement CreateCommands()
    {
        _instructionsToggle = AxisToggle("Instructions",
                                         TraceTimelineAxis.Instructions,
                                         "Width is the instructions each call executed, so it shows how long each call took");

        _positionToggle = AxisToggle("Position",
                                     TraceTimelineAxis.Position,
                                     "Width is the span of trace positions, which lines threads up against each other");

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        commands.Children.Add(_instructionsToggle);

        commands.Children.Add(_positionToggle);

        commands.Children.Add(CommandToggle(Label("Operators"),
                                            FlameChart.ShowOperators,
                                            "Show Plan Operators",
                                            OnOperatorsClick));

        _memoryToggle = CommandToggle(Label("Memory"),
                                      Settings.FlameChartShowMemory,
                                      "Display allocation/in-use memory",
                                      OnMemoryClick);

        commands.Children.Add(_memoryToggle);

        _playButton = CommandButton(new FontIcon { Glyph = FlameChart.IsPlaying ? PauseGlyph : PlayGlyph, FontSize = 12 },
                                    FlameChart.IsPlaying ? "Pause" : "Play",
                                    OnPlayClick);

        commands.Children.Add(_playButton);

        commands.Children.Add(CommandButton(new FontIcon { Glyph = StepBackGlyph, FontSize = 12 },
                                            "Previous Frame",
                                            OnStepBackClick));

        commands.Children.Add(CommandButton(new FontIcon { Glyph = StepForwardGlyph, FontSize = 12 },
                                            "Next Frame",
                                            OnStepForwardClick));

        _lockToggle = CommandToggle(new FontIcon { Glyph = FlameChart.IsLocked ? LockedGlyph : UnlockedGlyph, FontSize = 12 },
                                    FlameChart.IsLocked,
                                    "Lock chart to selection",
                                    OnLockClick);

        commands.Children.Add(_lockToggle);

        _searchToggle = CommandToggle(new FontIcon { Glyph = SearchGlyph, FontSize = 12 },
                                      false,
                                      "Search function names",
                                      OnSearchClick);

        commands.Children.Add(_searchToggle);

        return commands;
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        FlameChart.CallSelected -= OnCallSelected;

        FlameChart.SelectionCleared -= OnSelectionCleared;

        FlameChart.CallStackOpenRequested -= OnCallStackOpenRequested;

        FlameChart.PlanNodeSelected -= OnPlanNodeSelected;

        FlameChart.PlayStateChanged -= OnPlayStateChanged;

        FlameChart.LockChanged -= OnLockChanged;

        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        FlameChart.Dispose();
    }

    private ToggleButton AxisToggle(string text, TraceTimelineAxis axis, string toolTip)
    {
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = Label(text),
            IsChecked = FlameChart.Axis == axis,
            Tag = axis
        };

        ToolTipService.SetToolTip(toggle, toolTip);

        toggle.Click += OnAxisClick;

        return toggle;
    }

    private static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    private static ToggleButton CommandToggle(UIElement content, bool isChecked, string toolTip, RoutedEventHandler onClick)
    {
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = content,
            IsChecked = isChecked,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(toggle, toolTip);

        toggle.Click += onClick;

        return toggle;
    }

    private static Button CommandButton(UIElement content, string toolTip, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = content,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(button, toolTip);

        button.Click += onClick;

        return button;
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

    private void OnMemoryClick(object sender, RoutedEventArgs e) => Settings.FlameChartShowMemory = IsChecked(sender);

    private void OnOperatorsClick(object sender, RoutedEventArgs e) => FlameChart.ShowOperators = IsChecked(sender);

    private void OnPlayClick(object sender, RoutedEventArgs e) => FlameChart.TogglePlay();

    private void OnPlayStateChanged(bool isPlaying)
    {
        if (_playButton is not { Content: FontIcon icon } button)
        {
            return;
        }

        icon.Glyph = isPlaying ? PauseGlyph : PlayGlyph;

        ToolTipService.SetToolTip(button, isPlaying ? "Pause" : "Play");
    }

    private void OnStepBackClick(object sender, RoutedEventArgs e) => FlameChart.StepPlayhead(forward: false);

    private void OnStepForwardClick(object sender, RoutedEventArgs e) => FlameChart.StepPlayhead(forward: true);

    private void OnLockClick(object sender, RoutedEventArgs e) => FlameChart.IsLocked = IsChecked(sender);

    private void OnLockChanged(bool isLocked)
    {
        if (_lockToggle is not { Content: FontIcon icon } toggle)
        {
            return;
        }

        toggle.IsChecked = isLocked;

        icon.Glyph = isLocked ? LockedGlyph : UnlockedGlyph;
    }

    private static bool IsChecked(object sender) => sender is ToggleButton { IsChecked: true };

    private void OnSearchClick(object sender, RoutedEventArgs e) => ShowSearch(IsChecked(sender));

    private void ShowSearch(bool show)
    {
        SearchBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        FlameChart.SearchText = show ? SearchBar.Text : null;

        _searchToggle?.IsChecked = show;

        if (show)
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

        ShowSearch(false);

        e.Handled = true;
    }

    private void OnCallSelected(CallStackNode node, int call) => ViewModel?.NavigateToCall(node, call);

    private void OnCallStackOpenRequested(CallStackNode? node, int call) => ViewModel?.OpenCallStack(node, call);

    private void OnSelectionCleared()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.Arguments.SelectedCall = null;

        if (!FlameChart.IsLocked)
        {
            viewModel.SelectedCallNode = null;
        }
    }

    private void OnPlanNodeSelected(PlanNodeIdentifier identifier) => ViewModel?.SelectPlanNode(identifier);

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.CallTreeHiddenCategories))
        {
            FlameChart.HiddenCategories = SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories);
        }
        else if (e.PropertyName == nameof(SettingsViewModel.CallTreePrunedCategories))
        {
            FlameChart.PrunedCategories = SettingsViewModel.SplitCategories(Settings.CallTreePrunedCategories);
        }
        else if (e.PropertyName == nameof(SettingsViewModel.FlameChartShowMemory) && _memoryToggle is { } toggle)
        {
            toggle.IsChecked = Settings.FlameChartShowMemory;
        }
    }
}
