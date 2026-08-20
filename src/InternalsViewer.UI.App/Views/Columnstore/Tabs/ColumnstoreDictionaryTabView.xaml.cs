using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.Controls.Docking;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreDictionaryTabView : UserControl, IDocumentCommands, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public ColumnstoreDictionaryTabView()
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

    public DictionaryTabViewModel ViewModel => (DictionaryTabViewModel)DataContext;

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

        panel.Children.Add(derivation);
        panel.Children.Add(hex);

        return panel;
    }

    private void PageEntries_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectEntry(((TableView)sender).SelectedItem as DictionaryEntryDetail);
    }

    private void Decode_OnSymbolInvoked(object? sender, int symbol) => ViewModel.SelectSymbol(symbol);

    /// <summary>
    /// Brings the code table onto the symbol, the drawing and the tree both being able to move it
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DictionaryTabViewModel.SelectedSymbol))
        {
            return;
        }

        var selected = ViewModel.Codes.FirstOrDefault(c => c.Symbol == ViewModel.SelectedSymbol);

        if (!Equals(CodeTable.SelectedItem, selected))
        {
            CodeTable.SelectedItem = selected;
        }
    }

    private void CodeTable_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedSymbol = ((TableView)sender).SelectedItem is HuffmanCodeDetail code ? code.Symbol : -1;
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

        DecodeControl.Dispose();

        TreeControl.Dispose();

        _cts.Cancel();
        _cts.Dispose();
    }
}
