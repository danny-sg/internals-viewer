using InternalsViewer.Query.Results;
using InternalsViewer.Query.Results.Enums;
using CommunityToolkit.WinUI;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Foundation;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed partial class ResultsGridControl : UserControl
{
    private const double CellMargin = 12;

    private const double MaximumFitWidth = 640;

    private const string DetailsToggleName = "DetailsToggleButton";

    private const string DetailsToggleStyleKey = "DetailsToggleStyle";

    private static readonly Thickness GroupPadding = new(8, 0, 8, 0);

    private static readonly Thickness GroupBorder = new(0, 0, 1, 1);


    public static readonly DependencyProperty ResultSetProperty =
        DependencyProperty.Register(
            nameof(ResultSet),
            typeof(QueryResultSet),
            typeof(ResultsGridControl),
            new PropertyMetadata(null, OnResultSetChanged));

    public QueryResultSet? ResultSet
    {
        get => (QueryResultSet?)GetValue(ResultSetProperty);
        set => SetValue(ResultSetProperty, value);
    }

    public static readonly DependencyProperty SelectedRowProperty =
        DependencyProperty.Register(
            nameof(SelectedRow),
            typeof(ResultRow<long>),
            typeof(ResultsGridControl),
            new PropertyMetadata(null, OnSelectedRowChanged));

    public ResultRow<long>? SelectedRow
    {
        get => (ResultRow<long>?)GetValue(SelectedRowProperty);
        set => SetValue(SelectedRowProperty, value);
    }

    public static readonly DependencyProperty RowNameProperty =
        DependencyProperty.Register(
            nameof(RowName),
            typeof(string),
            typeof(ResultsGridControl),
            new PropertyMetadata("row", OnRowNameChanged));

    public string RowName
    {
        get => (string)GetValue(RowNameProperty);
        set => SetValue(RowNameProperty, value);
    }

    public static readonly DependencyProperty RowDetailsProperty =
        DependencyProperty.Register(
            nameof(RowDetails),
            typeof(Func<ResultRow<long>, ResultDetails>),
            typeof(ResultsGridControl),
            new PropertyMetadata(null, OnRowDetailsChanged));

    public Func<ResultRow<long>, ResultDetails>? RowDetails
    {
        get => (Func<ResultRow<long>, ResultDetails>?)GetValue(RowDetailsProperty);
        set => SetValue(RowDetailsProperty, value);
    }

    public static readonly DependencyProperty IsStatusBarVisibleProperty =
        DependencyProperty.Register(
            nameof(IsStatusBarVisible),
            typeof(bool),
            typeof(ResultsGridControl),
            new PropertyMetadata(true, OnIsStatusBarVisibleChanged));

    public bool IsStatusBarVisible
    {
        get => (bool)GetValue(IsStatusBarVisibleProperty);
        set => SetValue(IsStatusBarVisibleProperty, value);
    }

    public static readonly DependencyProperty VersionProperty =
        DependencyProperty.Register(
            nameof(Version),
            typeof(int),
            typeof(ResultsGridControl),
            new PropertyMetadata(0, OnVersionChanged));

    public int Version
    {
        get => (int)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    private Size _lastKnownSize = new(800, 600);

    public ResultsGridControl()
    {
        InitializeComponent();

        SizeChanged += (_, e) =>
        {
            if (e.NewSize is { Width: > 0, Height: > 0 })
            {
                _lastKnownSize = e.NewSize;
            }
        };
    }

    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public event EventHandler<ObjectValue>? ObjectClicked;

    public event EventHandler<CallValue>? CallClicked;

    public event EventHandler<ResultCellMenuEventArgs>? CellMenuOpening;

    public event EventHandler<ResultCellHoverEventArgs>? CellHovered;

    public event EventHandler<ResultSortEventArgs>? SortRequested;

    private TableView? ResultsTable { get; set; }

    private HashSet<Panel> DetailHosts { get; } = [];

    private bool IsRefreshing { get; set; }

    private List<ColumnGroup> Groups { get; } = [];

    private ScrollViewer? TableScroller { get; set; }

    private void Rebuild()
    {
        ReleaseTable();

        if (ResultSet is not { Columns: var columns, Rows: var rows })
        {
            StatusText.Text = string.Empty;
            return;
        }

        var table = (TableView)((DataTemplate)Resources["ResultsTableTemplate"]).LoadContent();

        var first = rows.Count > 0 ? rows[0] : null;

        foreach (var column in columns)
        {
            var resultCellColumn = new ResultCellColumn(column.Ordinal)
            {
                Header = CreateHeader(column),
                BackgroundColour = column.BackgroundColour,
                Width = GetColumnWidth(column, first, table.FontSize),
                Alignment = column.Alignment,
                PageClicked = OnPageClicked,
                ObjectClicked = ObjectClicked is null ? null : OnObjectClicked,
                CallClicked = CallClicked is null ? null : OnCallClicked,
                Hovered = CellHovered is null ? null : OnCellHovered,
                CanSort = column.IsSortable,
            };

            if (column.SortDirection is { } direction)
            {
                resultCellColumn.SortDirection = direction == ResultSortDirection.Ascending ? SortDirection.Ascending : SortDirection.Descending;
            }

            if (CreateHeaderStyle(column) is { } headerStyle)
            {
                resultCellColumn.HeaderStyle = headerStyle;
            }

            table.Columns.Add(resultCellColumn);
        }

        if (SortRequested is not null)
        {
            table.CanSortColumns = true;

            table.Sorting += OnSorting;

            table.ClearSorting += OnClearSorting;
        }

        BuildGroups(table, columns);

        if (RowDetails is not null)
        {
            table.RowDetailsTemplate = (DataTemplate)Resources["RowDetailsTemplate"];

            table.RowDetailsVisibilityMode = TableViewRowDetailsVisibilityMode.VisibleWhenExpanded;

            table.HeadersVisibility = TableViewHeadersVisibility.All;

            table.ContainerContentChanging += OnContainerContentChanging;
        }

        table.ItemsSource = rows;

        table.SelectionChanged += OnSelectionChanged;

        if (CellMenuOpening is not null)
        {
            table.CellContextFlyout = new MenuFlyout();

            table.CellContextFlyoutOpening += OnCellContextFlyoutOpening;
        }

        ResultsTable = table;

        TableHost.Child = table;

        UpdateStatus();

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, WarmRows);
    }

    /// <summary>
    /// Realises row containers while the tab is hidden, so the first switch to it does not pay for them
    /// </summary>
    /// <remarks>
    /// A collapsed element is skipped by the layout pass, so the grid holds no row containers until it is first shown.
    /// Measuring it explicitly at idle priority drives that work early, at the last size the control was actually shown
    /// at. If the grid is already visible the normal layout pass covers it and there is nothing to warm.
    /// </remarks>
    private void WarmRows()
    {
        if (Visibility == Visibility.Visible)
        {
            return;
        }

        ResultsTable?.Measure(GetWarmSize());
    }

    private void Refresh()
    {
        if (ResultsTable is { } table)
        {
            var selected = SelectedRow;

            IsRefreshing = true;

            try
            {
                table.RefreshView();
            }
            finally
            {
                IsRefreshing = false;
            }

            if (selected is not null && !ReferenceEquals(table.SelectedItem, selected))
            {
                table.SelectedItem = selected;
            }
        }

        foreach (var host in DetailHosts)
        {
            FillDetails(host);
        }
    }

    private void ReleaseTable()
    {
        DetailHosts.Clear();

        if (ResultsTable is not { } table)
        {
            return;
        }

        table.SelectionChanged -= OnSelectionChanged;

        table.CellContextFlyoutOpening -= OnCellContextFlyoutOpening;

        table.ContainerContentChanging -= OnContainerContentChanging;

        table.LayoutUpdated -= OnTableLayoutUpdated;

        table.Loaded -= OnTableLoaded;

        if (TableScroller is { } scroller)
        {
            scroller.ViewChanging -= OnTableScrollChanging;

            TableScroller = null;
        }

        Groups.Clear();

        GroupBand.Children.Clear();

        GroupBand.Visibility = Visibility.Collapsed;

        TableHost.Child = null;

        table.Sorting -= OnSorting;

        table.ClearSorting -= OnClearSorting;

        table.ItemsSource = null;

        ResultsTable = null;
    }

    private void BuildGroups(TableView table, IReadOnlyList<ResultColumn> columns)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Group is not { } name)
            {
                continue;
            }

            var last = index;

            while (last + 1 < columns.Count && columns[last + 1].Group == name)
            {
                last++;
            }

            var label = new Border
            {
                Height = GroupBand.Height,
                Padding = GroupPadding,
                BorderThickness = GroupBorder,
                BorderBrush = Resource<Brush>("ControlStrokeColorDefaultBrush"),
                Child = new TextBlock
                {
                    Text = name,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };

            GroupBand.Children.Add(label);

            Groups.Add(new ColumnGroup(table.Columns[index], table.Columns[last], label));

            index = last;
        }

        if (Groups.Count == 0)
        {
            return;
        }

        GroupBand.Visibility = Visibility.Visible;

        table.LayoutUpdated += OnTableLayoutUpdated;

        table.Loaded += OnTableLoaded;
    }

    private void PositionGroups()
    {
        foreach (var group in Groups)
        {
            if (group.First.HeaderControl is not { XamlRoot: not null, ActualWidth: > 0 } first
                || group.Last.HeaderControl is not { XamlRoot: not null } last)
            {
                continue;
            }

            var left = first.TransformToVisual(GroupBand).TransformPoint(default).X;

            var width = Math.Max(0, last.TransformToVisual(GroupBand).TransformPoint(default).X + last.ActualWidth - left);

            if (Canvas.GetLeft(group.Label) != left)
            {
                Canvas.SetLeft(group.Label, left);
            }

            if (group.Label.Width != width)
            {
                group.Label.Width = width;
            }
        }
    }

    private void OnTableLayoutUpdated(object? sender, object e) => PositionGroups();

    private void OnTableScrollChanging(object? sender, ScrollViewerViewChangingEventArgs e) => PositionGroups();

    private void OnTableLoaded(object sender, RoutedEventArgs e)
    {
        if (TableScroller is null && ((TableView)sender).FindDescendant<ScrollViewer>() is { } scroller)
        {
            TableScroller = scroller;

            scroller.ViewChanging += OnTableScrollChanging;
        }

        PositionGroups();
    }

    private void OnGroupBandSizeChanged(object sender, SizeChangedEventArgs e)
        => GroupBand.Clip = new RectangleGeometry { Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };

    private void UpdateStatus()
    {
        if (ResultSet is not { Rows.Count: var count })
        {
            StatusText.Text = string.Empty;

            return;
        }

        StatusText.Text = count == 1 ? $"1 {RowName}" : $"{count:N0} {RowName}s";
    }

    private void OnCellContextFlyoutOpening(object? sender, TableViewCellContextFlyoutEventArgs e)
    {
        if (e.Flyout is not MenuFlyout menu || e.Item is not ResultRow<long> row)
        {
            e.Handled = true;

            return;
        }

        menu.Items.Clear();

        CellMenuOpening?.Invoke(this, new ResultCellMenuEventArgs(row, e.Slot.Column, menu));

        e.Handled = menu.Items.Count == 0;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsRefreshing && ReferenceEquals(sender, ResultsTable))
        {
            SelectedRow = ResultsTable.SelectedItem as ResultRow<long>;
        }
    }

    /// <summary>
    /// The size to realise row containers against
    /// </summary>
    /// <remarks>
    /// Realising against the wrong height leaves the shortfall to be built on the first switch, which is the cost being
    /// avoided. The control itself has never been arranged when hidden, but the panel hosting it has, so its size is the
    /// closest available stand-in for the viewport.
    /// </remarks>
    private Size GetWarmSize()
    {
        if (Parent is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } host)
        {
            return new Size(host.ActualWidth, host.ActualHeight);
        }

        return _lastKnownSize;
    }

    private static GridLength GetColumnWidth(ResultColumn column, ResultRow<long>? first, double fontSize)
    {
        if (column.Width is null && first is not null)
        {
            var content = ResultCellColumn.MeasureWidth(first[column.Ordinal], fontSize) + CellMargin;

            return new GridLength(Math.Min(Math.Max(content, HeaderWidth(column)), MaximumFitWidth));
        }

        var width = Type.GetTypeCode(column.ClrType) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 => 60,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => 90,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => 100,
            TypeCode.DateTime => 160,
            _ => 140
        };

        return new GridLength(column.Width ?? Math.Max(width, HeaderWidth(column)));
    }

    private static int HeaderWidth(ResultColumn column) => HeaderLength(column) * 7 + 24;

    private static object CreateHeader(ResultColumn column)
    {
        if (column.TypeName is null && column.Detail is null)
        {
            return column.Name;
        }

        var header = new TextBlock();

        if (column.Name.Length > 0)
        {
            header.Inlines.Add(new Run { Text = $"{column.Name}  " });
        }

        if (column.TypeName is { } typeName)
        {
            header.Inlines.Add(new Run
            {
                Text = typeName,
                FontFamily = Resource<FontFamily>("MonospaceFontFamily"),
                Foreground = Resource<Brush>("CppClassBrush")
            });
        }

        if (column.Detail is { } detail)
        {
            header.Inlines.Add(new Run
            {
                Text = column.TypeName is null ? detail : $"  {detail}",
                Foreground = Resource<Brush>("TextFillColorTertiaryBrush")
            });
        }

        return header;
    }

    private static Style? CreateHeaderStyle(ResultColumn column)
    {
        if (column.BackgroundColour is not { } colour)
        {
            return null;
        }

        var style = new Style(typeof(TableViewColumnHeader)) { BasedOn = Resource<Style>("TableViewColumnHeaderStyle") };

        style.Setters.Add(new Setter(Control.BackgroundProperty,
                                     new SolidColorBrush(Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B))));

        return style;
    }

    private static int HeaderLength(ResultColumn column)
        => column.Name.Length + (column.TypeName?.Length + 2 ?? 0) + (column.Detail?.Length + 2 ?? 0);

    private static T? Resource<T>(string key) where T : class
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as T : null;

    private void OnPageClicked(PageAddressEventArgs e)
    {
        PageClicked?.Invoke(this, e);
    }

    private void OnObjectClicked(ObjectValue value) => ObjectClicked?.Invoke(this, value);

    private void OnCallClicked(CallValue value) => CallClicked?.Invoke(this, value);

    private void OnCellHovered(ResultCellHoverEventArgs e) => CellHovered?.Invoke(this, e);

    private void OnSorting(object? sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column is not ResultCellColumn column || FindColumn(column.Ordinal) is not { IsSortable: true } definition)
        {
            return;
        }

        var direction = definition.SortDirection == ResultSortDirection.Ascending
            ? ResultSortDirection.Descending
            : ResultSortDirection.Ascending;

        SortRequested?.Invoke(this, new ResultSortEventArgs(column.Ordinal, direction));
    }

    private void OnClearSorting(object? sender, TableViewClearSortingEventArgs e)
    {
        if (e.Column is ResultCellColumn column)
        {
            SortRequested?.Invoke(this, new ResultSortEventArgs(column.Ordinal, null));
        }
    }

    private ResultColumn? FindColumn(int ordinal)
    {
        foreach (var column in ResultSet?.Columns ?? [])
        {
            if (column.Ordinal == ordinal)
            {
                return column;
            }
        }

        return null;
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemContainer is not { } row || StyleDetailsToggle(row))
        {
            return;
        }

        row.Loaded -= OnRowLoaded;

        row.Loaded += OnRowLoaded;
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement row)
        {
            row.Loaded -= OnRowLoaded;

            StyleDetailsToggle(row);
        }
    }

    private bool StyleDetailsToggle(DependencyObject row)
    {
        if (FindDetailsToggle(row) is not { } toggle)
        {
            return false;
        }

        var style = (Style)Resources[DetailsToggleStyleKey];

        if (!ReferenceEquals(toggle.Style, style))
        {
            toggle.Style = style;
        }

        return true;
    }

    private static ToggleButton? FindDetailsToggle(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is ToggleButton { Name: DetailsToggleName } toggle)
            {
                return toggle;
            }

            if (child is not TableView && FindDetailsToggle(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnRowDetailsDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is not Panel host)
        {
            return;
        }

        DetailHosts.Add(host);

        FillDetails(host);
    }

    private void OnRowDetailsUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel host)
        {
            DetailHosts.Remove(host);
        }
    }

    private void FillDetails(Panel host)
    {
        if (host.DataContext is not ResultRow<long> row || RowDetails is not { } details)
        {
            host.Children.Clear();

            return;
        }

        ResultDetailsPanel.Fill(host, details(row));
    }

    private static void OnRowNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ResultsGridControl)d).UpdateStatus();

    private static void OnRowDetailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ResultsGridControl)d).Rebuild();

    private static void OnVersionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ResultsGridControl)d).Refresh();

    private static void OnIsStatusBarVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ResultsGridControl)d;

        control.StatusBar.Visibility = control.IsStatusBarVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnResultSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ResultsGridControl)d;
        
        control.SelectedRow = null;
        control.Rebuild();
    }

    private static void OnSelectedRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ResultsGridControl)d;

        if (control.ResultsTable is not { } table || ReferenceEquals(table.SelectedItem, e.NewValue))
        {
            return;
        }

        table.SelectedItem = e.NewValue;

        if (e.NewValue is { } row)
        {
            control.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                if (ReferenceEquals(control.ResultsTable, table))
                {
                    table.ScrollIntoView(row, ScrollIntoViewAlignment.Leading);
                }
            });
        }
    }

    private sealed record ColumnGroup(TableViewColumn First, TableViewColumn Last, Border Label);
}