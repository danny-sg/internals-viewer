using System;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreStructureTabView : IDisposable
{
    public ColumnstoreStructureTabView()
    {
        InitializeComponent();

        StructureControl.ElementClicked += OnElementClicked;

        BuildLegend();

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private void BuildLegend()
    {
        foreach (var (name, colour) in ColumnstoreLayout.Legend)
        {
            var swatch = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(255, colour.Red, colour.Green, colour.Blue))
            };

            var label = new TextBlock
            {
                Text = name,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.8
            };

            LegendItems.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 16, 0),
                Children = { swatch, label }
            });
        }
    }

    private void OnElementClicked(object? sender, ColumnstoreRegion region)
    {
        switch (region.ElementType)
        {
            case ColumnstoreElementType.Segment when region.Segment is { } segment:
                ViewModel.OpenSegment(segment);
                break;

            case ColumnstoreElementType.Dictionary when region.Dictionary is { } dictionary:
                ViewModel.OpenDictionary(dictionary);
                break;

            case ColumnstoreElementType.DeleteBitmap:
                ViewModel.OpenDeleteBitmap();
                break;

            case ColumnstoreElementType.DeltaStore when region.RowGroup is { } rowGroup:
                ViewModel.OpenDeltaStore(rowGroup);
                break;
        }
    }

    public void Dispose()
    {
        StructureControl.ElementClicked -= OnElementClicked;
        StructureControl.Dispose();
    }
}
