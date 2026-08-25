using System;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml.Controls;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs.Dictionary;

public sealed partial class DecodeTabView : UserControl, IDisposable
{
    private DictionaryTabViewModel? _tracked;

    /// <summary>
    /// Width the coding side is given back when a page that has coding is selected again
    /// </summary>
    /// <remarks>
    /// The splitter writes its own width into the column, so the width is remembered here before the column is
    /// taken away rather than being driven by a binding, which would overwrite a dragged width every time the
    /// bindings were refreshed.
    /// </remarks>
    private GridLength _huffmanWidth = new(2, GridUnitType.Star);

    private const double ValuesMinWidth = 240;

    public DecodeTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the tab sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public DictionaryTabViewModel ViewModel => (DictionaryTabViewModel)DataContext;

    /// <summary>
    /// Follows the view model the tab hands over, letting go of the one before it
    /// </summary>
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _tracked = DataContext as DictionaryTabViewModel;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged += OnViewModelPropertyChanged;
        }

        ApplyPanes();
    }

    /// <summary>
    /// Sizes the two halves to what is wanted and what there is, keeping a dragged width for when it is wanted again
    /// </summary>
    private void ApplyPanes()
    {
        var showDetails = (_tracked?.HasHuffmanPage ?? false) && (_tracked?.IsDecodeDetailsVisible ?? true);

        var showValues = _tracked?.IsDecodeValuesVisible ?? true;

        if (showDetails)
        {
            HuffmanColumn.Width = showValues ? _huffmanWidth : new GridLength(1, GridUnitType.Star);
        }
        else
        {
            if (showValues && HuffmanColumn.Width.IsStar && HuffmanColumn.Width.Value > 0)
            {
                _huffmanWidth = HuffmanColumn.Width;
            }

            HuffmanColumn.Width = new GridLength(0);
        }

        ValuesColumn.Width = showValues ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        ValuesColumn.MinWidth = showValues ? ValuesMinWidth : 0;

        PageEntries.Visibility = showValues ? Visibility.Visible : Visibility.Collapsed;

        Splitter.Visibility = showValues && showDetails ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Brings the code table onto the symbol, the drawing and the tree both being able to move it
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DictionaryTabViewModel.HasHuffmanPage)
                              or nameof(DictionaryTabViewModel.IsDecodeValuesVisible)
                              or nameof(DictionaryTabViewModel.IsDecodeDetailsVisible))
        {
            ApplyPanes();

            return;
        }

        if (e.PropertyName is nameof(DictionaryTabViewModel.SelectedEntry))
        {
            SelectEntry();

            return;
        }

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

    /// <summary>
    /// Brings the table onto the entry something else picked, such as a handle followed to its value
    /// </summary>
    private void SelectEntry()
    {
        var entry = ViewModel.SelectedEntry;

        if (Equals(PageEntries.SelectedItem, entry))
        {
            return;
        }

        PageEntries.SelectedItem = entry;

        if (entry is not null)
        {
            PageEntries.ScrollIntoView(entry);
        }
    }

    private void PageEntries_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectEntry(((TableView)sender).SelectedItem as DictionaryEntryDetail);
    }

    private void Decode_OnSymbolInvoked(object? sender, int symbol) => ViewModel.SelectSymbol(symbol);

    private void CodeTable_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedSymbol = ((TableView)sender).SelectedItem is HuffmanCodeDetail code ? code.Symbol : -1;
    }

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnViewModelPropertyChanged;

            _tracked = null;
        }

        Bindings.StopTracking();

        DecodeControl.Dispose();

        TreeControl.Dispose();
    }
}
