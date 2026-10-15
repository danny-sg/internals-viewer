using System;
using System.Linq;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallTreeBatchDetailsView : UserControl
{
    private const double IndexWidth = 70;

    private const double VectorWidth = 170;

    private const double HeaderFontSize = 11;

    public CallTreeBatchDetailsView()
    {
        InitializeComponent();
    }

    private static SolidColorBrush PureBadgeBrush { get; } = new(Windows.UI.Color.FromArgb(255, 29, 158, 117));

    private static SolidColorBrush PureBadgeTextBrush { get; } = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));

    private static Style HeaderStyle => field ??= CreateHeaderStyle();

    private TableView? VectorTable { get; set; }

    public void Show(BatchesViewModel batches, int call)
    {
        if (batches.LoadBatch(call) is not { } lineage)
        {
            ShowMessage("No Batch");

            return;
        }

        VectorTable = CreateVectorTable(batches, lineage);

        VectorTableHost.Child = VectorTable;

        SelectionTable.ItemsSource = BatchesViewModel.GetSelectionRows(lineage.Capture);

        var deepData = BatchesViewModel.GetDeepDataRows(lineage.Capture);

        DeepDataTable.ItemsSource = deepData;

        DeepDataSummary.Text = deepData.Count == 0
            ? "No Entries"
            : $"{deepData.Count:N0} {(deepData.Count == 1 ? "Entry" : "Entries")}, {SizeFormat.Format(deepData.Sum(d => (long)d.Length))}";

        MessageText.Visibility = Visibility.Collapsed;

        BatchTabs.Visibility = Visibility.Visible;
    }

    public void Clear()
    {
        VectorTable = null;

        VectorTableHost.Child = null;

        SelectionTable.ItemsSource = null;

        DeepDataTable.ItemsSource = null;
    }

    public void Refresh()
    {
        if (VectorTable is not { } table)
        {
            return;
        }

        var selected = table.SelectedItem;

        table.RefreshView();

        if (selected is not null && !ReferenceEquals(table.SelectedItem, selected))
        {
            table.SelectedItem = selected;
        }
    }

    private static Style CreateHeaderStyle()
    {
        var style = new Style(typeof(TableViewColumnHeader));

        if (Application.Current.Resources.TryGetValue("TableViewColumnHeaderStyle", out var resource) && resource is Style basedOn)
        {
            style.BasedOn = basedOn;
        }

        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));

        return style;
    }

    private static StackPanel CreateHeader(UIElement top, string name)
    {
        var panel = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        panel.Children.Add(top);

        panel.Children.Add(new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis });

        return panel;
    }

    private static StackPanel CreateVectorHeader(BatchCaptureVector vector)
    {
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        label.Children.Add(new TextBlock
        {
            Text = $"Vector {vector.VectorId}",
            FontSize = HeaderFontSize,
            Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (vector.IsPure)
        {
            label.Children.Add(new Border
            {
                Padding = new Thickness(5, 1, 5, 1),
                CornerRadius = new CornerRadius(3),
                Background = PureBadgeBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = "Pure", FontSize = 10, Foreground = PureBadgeTextBrush }
            });
        }

        return label;
    }

    private void ShowMessage(string message)
    {
        Clear();

        MessageText.Text = message;

        MessageText.Visibility = Visibility.Visible;

        BatchTabs.Visibility = Visibility.Collapsed;
    }

    private TableView CreateVectorTable(BatchesViewModel batches, BatchLineage lineage)
    {
        var capture = lineage.Capture;

        var table = (TableView)((DataTemplate)Resources["VectorTableTemplate"]).LoadContent();

        table.Columns.Add(new TableViewTextColumn
        {
            Header = CreateHeader(new TextBlock { FontSize = HeaderFontSize }, "Index"),
            HeaderStyle = HeaderStyle,
            Width = new GridLength(IndexWidth),
            Binding = new Binding { Path = new PropertyPath(nameof(Models.Query.CallStack.BatchSlotRow.RowIndex)) }
        });

        for (var ordinal = 0; ordinal < capture.Vectors.Count; ordinal++)
        {
            table.Columns.Add(new BatchSlotColumn(batches, ordinal)
            {
                Header = CreateHeader(CreateVectorHeader(capture.Vectors[ordinal]), batches.GetColumnName(capture, ordinal)),
                HeaderStyle = HeaderStyle,
                Width = new GridLength(VectorWidth)
            });
        }

        table.ItemsSource = BatchesViewModel.GetVectorRows(lineage);

        return table;
    }
}
