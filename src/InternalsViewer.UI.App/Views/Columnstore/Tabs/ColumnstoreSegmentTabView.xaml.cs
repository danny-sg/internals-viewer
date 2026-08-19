using System;
using System.ComponentModel;
using System.Threading;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreSegmentTabView : UserControl, IDocumentCommands, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public ColumnstoreSegmentTabView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += (_, _) =>
        {
            Bindings.Update();

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SegmentTabViewModel.Region))
        {
            SelectRegionTab(ViewModel.Region);
        }
    }

    /// <summary>
    /// Selects the tab for the region, the window having scrolled into it rather than the tab having been picked
    /// </summary>
    private void SelectRegionTab(SegmentRegion region)
    {
        foreach (var item in RegionTabView.TabItems)
        {
            if (item is TabViewItem { Tag: string tag } tab && tag == region.ToString())
            {
                RegionTabView.SelectedItem = tab;

                return;
            }
        }
    }

    public SegmentTabViewModel ViewModel => (SegmentTabViewModel)DataContext;

    /// <summary>
    /// The hex view is either at its set width or hidden, a splitter being of no use to a fixed width column
    /// </summary>
    public FrameworkElement CreateCommands()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Hex",
            IsChecked = ViewModel.IsHexViewVisible
        };

        toggle.Checked += (_, _) => ViewModel.IsHexViewVisible = true;
        toggle.Unchecked += (_, _) => ViewModel.IsHexViewVisible = false;

        var auto = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Auto",
            IsChecked = ViewModel.IsAutoRegion
        };

        ToolTipService.SetToolTip(auto, "Move on to the tab for the region scrolled into");

        auto.Checked += (_, _) => ViewModel.IsAutoRegion = true;
        auto.Unchecked += (_, _) => ViewModel.IsAutoRegion = false;

        panel.Children.Add(auto);
        panel.Children.Add(toggle);

        return panel;
    }

    private void TabView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((TabView)sender).SelectedItem is TabViewItem { Tag: string tag }
            && Enum.TryParse<SegmentRegion>(tag, out var region))
        {
            ViewModel.Region = region;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load(_cts.Token);
    }

    public void Dispose()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ViewModel.Dispose();

        _cts.Cancel();
        _cts.Dispose();
    }
}
