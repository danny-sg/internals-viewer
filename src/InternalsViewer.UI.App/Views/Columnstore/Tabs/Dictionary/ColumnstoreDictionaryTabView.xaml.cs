using System;
using System.Threading;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.Controls.Docking;
using Microsoft.UI.Xaml.Controls;
using WinUI.TableView;
using Microsoft.UI.Xaml.Controls.Primitives;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using Microsoft.UI.Xaml.Input;
using InternalsViewer.UI.App.Controls.Columnstore;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDictionaryTabView : UserControl, IDocumentCommands, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private DictionaryTabViewModel? _tracked;

    public ColumnstoreDictionaryTabView()
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

        _tracked = DataContext as DictionaryTabViewModel;
    }

    public DictionaryTabViewModel ViewModel => (DictionaryTabViewModel)DataContext;

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

        var derivation = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Show Derivation",
            IsChecked = ViewModel.IsDerivationVisible
        };

        ToolTipService.SetToolTip(derivation, "Show the working behind a value rather than the value alone");

        derivation.Checked += (_, _) => ViewModel.IsDerivationVisible = true;
        derivation.Unchecked += (_, _) => ViewModel.IsDerivationVisible = false;

        var hex = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = "Hex",
            IsChecked = ViewModel.Hex.IsVisible
        };

        hex.Checked += (_, _) => ViewModel.Hex.IsVisible = true;
        hex.Unchecked += (_, _) => ViewModel.Hex.IsVisible = false;

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
        panel.Children.Add(hex);

        return panel;
    }

    private void Handles_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectHandle(((TableView)sender).SelectedItem as DictionaryHandleDetail);
    }

    /// <summary>
    /// Follows a handle to the page holding its value, which the decode tab opens on the entry itself
    /// </summary>
    private void HandlePage_OnClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is DictionaryHandleDetail handle)
        {
            _ = ViewModel.GoToHandleValue(handle);
        }
    }

    private void CommandBar_OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var flyout = CsIndexMenu.Build(ViewModel.Dictionary.IsGlobal ? "Global" : $"Local {ViewModel.Dictionary.DictionaryId}",
                                       ViewModel.GetCsIndexCommand);

        flyout?.ShowAt(DictionaryCommandBar, e.GetPosition(DictionaryCommandBar));
    }

    private void Entries_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectEntry(((TableView)sender).SelectedItem as DictionaryEntryDetail);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        await ViewModel.Load(_cts.Token);
    }

    public void Dispose()
    {
        Loaded -= OnLoaded;

        DataContextChanged -= OnDataContextChanged;

        if (_tracked is not null)
        {
            _tracked.Dispose();

            _tracked = null;
        }

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        DecodeTab.Dispose();

        _cts.Cancel();
        _cts.Dispose();
    }
}
