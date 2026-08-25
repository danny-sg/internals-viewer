using System;
using System.ComponentModel;
using System.Threading;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.ViewModels.Columnstore.Segment;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using WinUI.TableView;
using InternalsViewer.UI.App.Services.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using System.Collections;
using InternalsViewer.UI.App.Controls;
using System.Collections.Generic;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreSegmentTabView : UserControl, IDocumentCommands, IDerivationNavigator, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Queue<string> _warming = new();

    private SegmentTabViewModel? _tracked;

    private ILogger? _logger;

    public ColumnstoreSegmentTabView()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public SegmentTabViewModel ViewModel => (SegmentTabViewModel)DataContext;

    /// <summary>
    /// Width the hex view takes beside the tabs
    /// </summary>
    public double HexWidth => 455;

    private ILogger Logger => _logger ??= App.GetService<ILoggerFactory>().CreateLogger<ColumnstoreSegmentTabView>();

    /// <summary>
    /// What the spinner is pushed across by, so it centres on the tabs rather than on the pair of them
    /// </summary>
    public GridLength GetHexSpacing(bool isLoaded, bool isHexVisible)
        => new(isLoaded && isHexVisible ? HexWidth : 0);

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

    public void OnStepInvoked(DerivationStep step)
    {
        if (step.Target is SegmentNavigationTarget target)
        {
            ViewModel.GoToTarget(target);
        }
    }

    /// <summary>
    /// Takes an operand back to where it was read from, which puts its region on show with the item selected
    /// </summary>
    public void OnResultInvoked(ValueDerivation derivation)
    {
        if (derivation.Target is SegmentNavigationTarget target)
        {
            ViewModel.GoToTarget(target);
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
            using var timing = Logger.Time("Realize panel", name);

            FindName(name);
        }
    }

    /// <summary>
    /// Reports what the grid on show has realised, which is how a grid that has stopped virtualizing gives itself up
    /// </summary>
    /// <remarks>
    /// A virtualizing panel keeps containers for the rows on screen and a cache either side of them. One holding a
    /// container per row has been measured against a height it can always satisfy, so it built the lot.
    /// </remarks>
    private void LogGridState()
    {
        if (!Logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (FindTableView(this) is not { } table)
            {
                return;
            }

            var panel = table.ItemsPanelRoot;

            var stack = panel as ItemsStackPanel;

            Logger.LogDebug("Grid {Rows} rows, {Containers} containers in {Panel}, visible {First} to {Last}, "
                            + "cached {CacheFirst} to {CacheLast}, height {Height:0}",
                            (table.ItemsSource as ICollection)?.Count ?? -1,
                            panel?.Children.Count ?? -1,
                            panel?.GetType().Name ?? "none",
                            stack?.FirstVisibleIndex ?? -1,
                            stack?.LastVisibleIndex ?? -1,
                            stack?.FirstCacheIndex ?? -1,
                            stack?.LastCacheIndex ?? -1,
                            table.ActualHeight);
        });
    }

    /// <summary>
    /// The grid the tab on show is holding, whichever panel that turns out to be
    /// </summary>
    private static TableView? FindTableView(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is TableView { ActualHeight: > 0 } table)
            {
                return table;
            }

            if (FindTableView(child) is { } found)
            {
                return found;
            }
        }

        return null;
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
        Logger.TimeUntilIdle(DispatcherQueue,
                             "Region tab",
                             ((TabView)sender).SelectedItem is TabViewItem { Tag: string name } ? name : null);

        ViewModel.Hex.SelectedMarker = null;

        LogGridState();

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

        WarmPanels();
    }

    /// <summary>
    /// Lays out the tabs that are not showing, so the first switch to one is not the first time it is measured
    /// </summary>
    /// <remarks>
    /// A collapsed panel is never measured, so its grid holds no rows until the tab is picked and the whole cost
    /// of generating them lands on the switch. Measuring each one here spends the same time while the reader is
    /// still on the header, and the containers it builds are what the switch then reuses.
    /// </remarks>
    private void WarmPanels()
    {
        (string Name, bool IsWanted)[] panels =
        [
            (nameof(BookmarksPanel), ViewModel.HasBookmarks),
            (nameof(RlePanel), ViewModel.HasRleArray),
            (nameof(BitPackPanel), ViewModel.HasBitpackArray),
            (nameof(VariableLengthDataPanel), ViewModel.HasVariableLengthData),
            (nameof(DataPanel), true)
        ];

        foreach (var (name, isWanted) in panels)
        {
            if (isWanted)
            {
                _warming.Enqueue(name);
            }
        }

        ViewModel.IsPreparing = _warming.Count > 0;

        WarmNext();
    }

    /// <summary>
    /// Takes the panels one at a time, so what the interface is doing between them is still its own
    /// </summary>
    /// <remarks>
    /// Only the first is waited on. The tab on show when a segment opens is the header, which has no grid to
    /// build, so the rest are laid out behind a view the reader can already use rather than behind a spinner.
    /// </remarks>
    private void WarmNext()
    {
        if (_warming.Count == 0)
        {
            ViewModel.IsPreparing = false;

            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            Warm(_warming.Dequeue());

            ViewModel.IsPreparing = false;

            WarmNext();
        });
    }

    /// <summary>
    /// Measures one panel out of sight, which is what leaves its grid holding the rows a switch would build
    /// </summary>
    private void Warm(string name)
    {
        if (FindName(name) is not FrameworkElement { Visibility: Visibility.Collapsed } panel)
        {
            return;
        }

        using var timing = Logger.Time("Warm panel", name);

        panel.Opacity = 0;

        panel.Visibility = Visibility.Visible;

        panel.UpdateLayout();

        panel.Visibility = Visibility.Collapsed;

        panel.Opacity = 1;
    }
}
