using System;
using System.Collections.Generic;
using System.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Summary;

public sealed partial class QueryTraceSummaryTabView : UserControl, IDisposable
{
    private const string HeaderTemplateKey = "PaneHeaderTemplate";

    public QueryTraceSummaryTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Loaded += OnLoaded;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private TraceSummaryViewModel? Attached { get; set; }

    private List<TraceSummaryPaneView> PaneViews { get; } = [];

    public void Dispose() => Attach(null);

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Attach(ViewModel?.TraceSummary);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Attach(ViewModel?.TraceSummary);

        Attached?.Show();
    }

    private void Attach(TraceSummaryViewModel? summary)
    {
        if (ReferenceEquals(summary, Attached))
        {
            return;
        }

        foreach (var view in PaneViews)
        {
            view.Pane.PropertyChanged -= OnPanePropertyChanged;

            view.Dispose();
        }

        PaneViews.Clear();

        PaneGrid.Children.Clear();

        PaneGrid.RowDefinitions.Clear();

        Attached = summary;

        if (summary is null)
        {
            return;
        }

        var template = (DataTemplate)Resources[HeaderTemplateKey];

        foreach (var pane in summary.Panes)
        {
            var header = new ContentControl
            {
                Content = pane,
                ContentTemplate = template,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsTabStop = false
            };

            var view = new TraceSummaryPaneView(pane);

            Grid.SetRow(header, PaneGrid.RowDefinitions.Count);

            PaneGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(view, PaneGrid.RowDefinitions.Count);

            PaneGrid.RowDefinitions.Add(new RowDefinition());

            PaneGrid.Children.Add(header);

            PaneGrid.Children.Add(view);

            PaneViews.Add(view);

            pane.PropertyChanged += OnPanePropertyChanged;
        }

        UpdateRows();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraceSummaryPaneViewModel.IsExpanded))
        {
            UpdateRows();
        }
    }

    private void UpdateRows()
    {
        for (var index = 0; index < PaneViews.Count; index++)
        {
            var isExpanded = PaneViews[index].Pane.IsExpanded;

            PaneGrid.RowDefinitions[(index * 2) + 1].Height = isExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            PaneViews[index].Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
