using System;
using System.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.Views.Query.Tabs.FlameChart;

public sealed partial class QueryFlameChartTabView : UserControl, IDocumentCommands, IDisposable
{
    private const string LockedGlyph = "";

    private const string UnlockedGlyph = "";

    private ToggleButton? _instructionsToggle;

    private ToggleButton? _positionToggle;

    private ToggleButton? _lockToggle;

    public QueryFlameChartTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();

        FlameChart.CallSelected += OnCallSelected;

        FlameChart.SelectionCleared += OnSelectionCleared;

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

        _lockToggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = new FontIcon { Glyph = FlameChart.IsLocked ? LockedGlyph : UnlockedGlyph, FontSize = 12 },
            IsChecked = FlameChart.IsLocked,
            Margin = new Thickness(6, 0, 0, 0)
        };

        ToolTipService.SetToolTip(_lockToggle,
                                  "Keep the flame chart where it is. Clicking a call still selects it in the call stack, "
                                  + "but does not change what the flame chart is rooted on.");

        _lockToggle.Click += OnLockClick;

        var fit = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = new TextBlock { Text = "Zoom To Fit", VerticalAlignment = VerticalAlignment.Center },
            Margin = new Thickness(6, 0, 0, 0)
        };

        ToolTipService.SetToolTip(fit,
                                  "Show every call. The wheel zooms, Shift+Wheel pans, Ctrl+Wheel scrolls the threads, "
                                  + "drag a rectangle to zoom into it, middle-drag pans");

        fit.Click += OnZoomToFitClick;

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
        commands.Children.Add(_lockToggle);
        commands.Children.Add(fit);

        return commands;
    }

    public void Dispose()
    {
        Bindings.StopTracking();

        FlameChart.CallSelected -= OnCallSelected;

        FlameChart.SelectionCleared -= OnSelectionCleared;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        FlameChart.Dispose();
    }

    private ToggleButton AxisToggle(string text, TimeTravelTimelineAxis axis, string toolTip)
    {
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
            IsChecked = FlameChart.Axis == axis,
            Tag = axis
        };

        ToolTipService.SetToolTip(toggle, toolTip);

        toggle.Click += OnAxisClick;

        return toggle;
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

    private void OnZoomToFitClick(object sender, RoutedEventArgs e) => FlameChart.ZoomToFit();

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        FlameChart.IsLocked = _lockToggle?.IsChecked == true;

        if (_lockToggle?.Content is FontIcon icon)
        {
            icon.Glyph = FlameChart.IsLocked ? LockedGlyph : UnlockedGlyph;
        }
    }

    private void OnCallSelected(CallStackNode node, int call) => ViewModel?.NavigateToCall(node, call);

    private void OnSelectionCleared() => ViewModel?.SelectedCallNode = null;

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.CallTreeHiddenCategories))
        {
            FlameChart.HiddenCategories = SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories);
        }
    }
}
