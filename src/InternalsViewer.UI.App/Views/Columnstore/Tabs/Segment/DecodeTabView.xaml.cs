using System;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs.Segment;

/// <summary>
/// The decompressed payload of the selected value store page beside the values read out of it
/// </summary>
public sealed partial class DecodeTabView : UserControl, IDisposable
{
    public DecodeTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the tab sets DataContext after the view is built
        DataContextChanged += OnDataContextChanged;
    }

    public SegmentTabViewModel ViewModel => (SegmentTabViewModel)DataContext;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    private void Values_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectValue(((TableView)sender).SelectedItem as ValueDetail);
    }

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();
    }
}
