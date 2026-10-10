using System;
using System.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
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

    private ToggleButton? _instructionsToggle;

    private ToggleButton? _positionToggle;

    private ToggleButton? _searchToggle;

    public QueryFlameChartTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        FlameChart.CallSelected += OnCallSelected;

        FlameChart.SelectionCleared += OnSelectionCleared;

        FlameChart.PlanNodeSelected += OnPlanNodeSelected;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        FlameChart.HiddenCategories = SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories);
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    public FrameworkElement CreateCommands()
    {
        _instructionsToggle = AxisToggle("Instructions",
                                         TimeTravelTimelineAxis.Instructions,
                                         "Width is the instructions each call executed, so it shows how long each call took");

        _positionToggle = AxisToggle("Position",
                                     TimeTravelTimelineAxis.Position,
                                     "Width is the span of trace positions, which lines threads up against each other");

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        _searchToggle = CommandToggle(new FontIcon { Glyph = SearchGlyph, FontSize = 12 },
                                      false,
                                      "Search the functions by name and dim every call that does not match",
                                      OnSearchClick);

        commands.Children.Add(_searchToggle);

        commands.Children.Add(_instructionsToggle);

        commands.Children.Add(_positionToggle);

        commands.Children.Add(CommandToggle(Label("Operators"),
                                            FlameChart.ShowOperators,
                                            "Show each plan operator from its first call to its last above the threads. "
                                            + "Drag the splitter to make the rows taller, click an operator to pick out its calls "
                                            + "and everything they call.",
                                            OnOperatorsClick));

        commands.Children.Add(CommandToggle(Label("Memory"),
                                            FlameChart.ShowMemory,
                                            "Raise the calls by memory, either as Allocated or In Use chosen above the ruler, and show the "
                                            + "memory allocated and in use along the bottom",
                                            OnMemoryClick));

        commands.Children.Add(CommandButton(new FontIcon { Glyph = StepBackGlyph, FontSize = 12 },
                                            "Move the playhead back to the start of the previous frame",
                                            OnStepBackClick));

        commands.Children.Add(CommandButton(new FontIcon { Glyph = StepForwardGlyph, FontSize = 12 },
                                            "Move the playhead on to the start of the next frame",
                                            OnStepForwardClick));

        commands.Children.Add(CommandToggle(new FontIcon { Glyph = FlameChart.IsLocked ? LockedGlyph : UnlockedGlyph, FontSize = 12 },
                                            FlameChart.IsLocked,
                                            "Keep the flame chart where it is. Clicking a call still selects it in the call stack, "
                                            + "but does not change what the flame chart is rooted on.",
                                            OnLockClick));

        return commands;
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        FlameChart.CallSelected -= OnCallSelected;

        FlameChart.SelectionCleared -= OnSelectionCleared;

        FlameChart.PlanNodeSelected -= OnPlanNodeSelected;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        FlameChart.Dispose();
    }

    private ToggleButton AxisToggle(string text, TimeTravelTimelineAxis axis, string toolTip)
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
            Margin = new Thickness(6, 0, 0, 0)
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
            Margin = new Thickness(6, 0, 0, 0)
        };

        ToolTipService.SetToolTip(button, toolTip);

        button.Click += onClick;

        return button;
    }

    private void OnAxisClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: TimeTravelTimelineAxis axis })
        {
            return;
        }

        FlameChart.Axis = axis;

        _instructionsToggle?.IsChecked = axis == TimeTravelTimelineAxis.Instructions;

        _positionToggle?.IsChecked = axis == TimeTravelTimelineAxis.Position;
    }

    private void OnMemoryClick(object sender, RoutedEventArgs e) => FlameChart.ShowMemory = IsChecked(sender);

    private void OnOperatorsClick(object sender, RoutedEventArgs e) => FlameChart.ShowOperators = IsChecked(sender);

    private void OnStepBackClick(object sender, RoutedEventArgs e) => FlameChart.StepPlayhead(forward: false);

    private void OnStepForwardClick(object sender, RoutedEventArgs e) => FlameChart.StepPlayhead(forward: true);

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        FlameChart.IsLocked = IsChecked(sender);

        if (sender is ToggleButton { Content: FontIcon icon })
        {
            icon.Glyph = FlameChart.IsLocked ? LockedGlyph : UnlockedGlyph;
        }
    }

    private static bool IsChecked(object sender) => sender is ToggleButton { IsChecked: true };

    private void OnSearchClick(object sender, RoutedEventArgs e) => ShowSearch(IsChecked(sender));

    private void ShowSearch(bool show)
    {
        SearchBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        FlameChart.SearchText = show ? SearchBox.Text : null;

        _searchToggle?.IsChecked = show;

        if (show)
        {
            SearchBox.Focus(FocusState.Programmatic);
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        => FlameChart.SearchText = sender.Text;

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

    private void OnSelectionCleared() => ViewModel?.SelectedCallNode = null;

    private void OnPlanNodeSelected(PlanNodeIdentifier identifier) => ViewModel?.SelectPlanNode(identifier);

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.CallTreeHiddenCategories))
        {
            FlameChart.HiddenCategories = SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories);
        }
    }
}
