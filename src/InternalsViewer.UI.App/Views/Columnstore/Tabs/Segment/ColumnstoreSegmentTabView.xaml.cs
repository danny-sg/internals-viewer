using System;
using System.ComponentModel;
using System.Threading;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreSegmentTabView : UserControl, IDocumentCommands, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private SegmentTabViewModel? _tracked;

    public ColumnstoreSegmentTabView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Follows the view model the dock hands over, letting go of the one before it
    /// </summary>
    /// <remarks>
    /// The event fires more than once over the life of a view, so subscribing without releasing the previous one
    /// leaves handlers stacked up on the view model and every change doing its work several times over.
    /// </remarks>
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _tracked = DataContext as SegmentTabViewModel;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged += OnViewModelPropertyChanged;
        }

        RealizePanels();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SegmentTabViewModel.Region))
        {
            SelectRegionTab(ViewModel.Region);
        }

        RealizePanels();
    }

    private void RealizePanels()
    {
        if (_tracked is not { } viewModel)
        {
            return;
        }

        Realize(ContentGrid, nameof(ContentGrid), viewModel.IsLoaded);
        Realize(BookmarksPanel, nameof(BookmarksPanel), viewModel.HasBookmarks);
        Realize(RlePanel, nameof(RlePanel), viewModel.HasRleArray);
        Realize(BitPackPanel, nameof(BitPackPanel), viewModel.HasBitpackArray);
        Realize(VariableLengthDataPanel, nameof(VariableLengthDataPanel), viewModel.HasVariableLengthData);
        Realize(DataPanel, nameof(DataPanel), viewModel.IsDataTabLoaded);
    }

    private void Realize(object? element, string name, bool isWanted)
    {
        if (element is null && isWanted)
        {
            FindName(name);
        }
    }

    /// <summary>
    /// Selects the tab for the region, the window having scrolled into it rather than the tab having been picked
    /// </summary>
    private void SelectRegionTab(SegmentRegion region)
    {
        if (RegionTabView is null)
        {
            return;
        }

        foreach (var item in RegionTabView.TabItems)
        {
            // A region with nothing in it has no tab, so a scroll that lands in one leaves the selection alone
            if (item is TabViewItem { Tag: string tag, Visibility: Visibility.Visible } tab && tag == region.ToString())
            {
                RegionTabView.SelectedItem = tab;

                return;
            }
        }
    }

    public SegmentTabViewModel ViewModel => (SegmentTabViewModel)DataContext;

    public Visibility GetTabContentVisibility(int selectedIndex, int index)
        => selectedIndex == index ? Visibility.Visible : Visibility.Collapsed;

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
            IsChecked = ViewModel.Hex.IsVisible
        };

        toggle.Checked += (_, _) => ViewModel.Hex.IsVisible = true;
        toggle.Unchecked += (_, _) => ViewModel.Hex.IsVisible = false;

        var derivation = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Show Derivation",
            IsChecked = ViewModel.IsDerivationVisible
        };

        ToolTipService.SetToolTip(derivation, "Show the working behind a value rather than the value alone");

        derivation.Checked += (_, _) => ViewModel.IsDerivationVisible = true;
        derivation.Unchecked += (_, _) => ViewModel.IsDerivationVisible = false;

        var auto = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Auto",
            IsChecked = ViewModel.IsAutoRegion
        };

        ToolTipService.SetToolTip(auto, "Move on to the tab for the region scrolled into");

        auto.Checked += (_, _) => ViewModel.IsAutoRegion = true;
        auto.Unchecked += (_, _) => ViewModel.IsAutoRegion = false;

        panel.Children.Add(derivation);
        panel.Children.Add(auto);
        panel.Children.Add(toggle);

        return panel;
    }

    /// <summary>
    /// Takes an operand back to where it was read from, which puts its region on show with the item selected
    /// </summary>
    private void Derivation_OnResultInvoked(object? sender, ValueDerivation derivation)
    {
        if (derivation.Target is SegmentNavigationTarget target)
        {
            ViewModel.GoToTarget(target);
        }
    }

    private void Derivation_OnStepInvoked(object? sender, DerivationStep step)
    {
        if (step.Target is SegmentNavigationTarget target)
        {
            ViewModel.GoToTarget(target);
        }
    }

    /// <summary>
    /// The store header is not a row of the page list, so its tab is what brings the window back to it
    /// </summary>
    private void VariableLengthDataTabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((TabView)sender).SelectedItem is not TabViewItem { Tag: string tag })
        {
            return;
        }

        if (tag == "StoreHeader")
        {
            ViewModel.GoToVariableLengthDataHeader();
        }
        else if (tag == "Decode")
        {
            ViewModel.SelectPayloadMarker();
        }
    }

    private void RleRunMap_OnRunInvoked(object? sender, SegmentNavigationTarget target) => ViewModel.GoToTarget(target);

    private void Marker_OnAddressClicked(object? sender, string address) => ViewModel.GoToValue(address);

    private void Bookmarks_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectBookmark(((TableView)sender).SelectedItem as BookmarkDetail);
    }

    private void RleValue_OnClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not RleRunDetail run)
        {
            return;
        }

        if (run.Address is { } address)
        {
            ViewModel.GoToValue(address.ToString());

            return;
        }

        ViewModel.GoToBitpackValue(run.Value);
    }

    private void BitpackUnits_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectBitpackUnit(((TableView)sender).SelectedItem as BitpackUnitRow);
    }

    private void RleRuns_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectRun(((TableView)sender).SelectedItem as RleRunDetail);
    }

    private void Dictionary_OnClick(object sender, RoutedEventArgs e) => ViewModel.OpenDictionary();

    private void CommandBar_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var flyout = CsIndexMenu.Build(ViewModel.Segment.ColumnName, ViewModel.GetCsIndexCommand);

        flyout?.ShowAt(SegmentCommandBar, e.GetPosition(SegmentCommandBar));
    }

    private void DataRows_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectRow(((TableView)sender).SelectedItem as SegmentRowDetail);
    }

    private void TabView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.Hex.SelectedMarker = null;

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

        if (RegionTabView is not null)
        {
            RegionTabView.SelectedIndex = 0;
        }
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        DataContextChanged -= OnDataContextChanged;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;

            _tracked.Dispose();

            _tracked = null;
        }

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        BitPackDetail?.Dispose();

        RleRunMap?.Dispose();

        DecodeTab?.Dispose();

        _cts.Cancel();
        _cts.Dispose();
    }
}
