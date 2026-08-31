using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

public sealed partial class QueryHistoryControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(QueryHistoryViewModel), typeof(QueryHistoryControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public QueryHistoryViewModel? ViewModel
    {
        get => (QueryHistoryViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public QueryHistoryControl()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? QuerySelected;

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.SearchText = sender.Text ?? string.Empty;
        }
    }

    private void OnEntryDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueryHistoryEntry entry })
        {
            QuerySelected?.Invoke(this, entry.Sql);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QueryHistoryEntry entry })
        {
            ViewModel?.Remove(entry);
        }
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((QueryHistoryControl)d).Bindings.Update();
}
