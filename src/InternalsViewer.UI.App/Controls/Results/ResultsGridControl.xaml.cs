using InternalsViewer.Query.Results;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed partial class ResultsGridControl : UserControl
{
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

    private TableView? ResultsTable { get; set; }

    private void Rebuild()
    {
        ReleaseTable();

        if (ResultSet is not { Columns: var columns, Rows: var rows })
        {
            StatusText.Text = string.Empty;
            return;
        }

        var table = (TableView)((DataTemplate)Resources["ResultsTableTemplate"]).LoadContent();

        foreach (var column in columns)
        {
            var resultCellColumn = new ResultCellColumn(column.Ordinal)
            {
                Header = CreateHeader(column),
                BackgroundColour = column.BackgroundColour,
                Width = GetColumnWidth(column),
                Alignment = column.Alignment,
                PageClicked = OnPageClicked,
            };

            if (CreateHeaderStyle(column) is { } headerStyle)
            {
                resultCellColumn.HeaderStyle = headerStyle;
            }

            table.Columns.Add(resultCellColumn);
        }

        table.ItemsSource = rows;

        table.SelectionChanged += OnSelectionChanged;

        ResultsTable = table;

        TableHost.Child = table;

        StatusText.Text = rows.Count == 1 ? "1 row" : $"{rows.Count:N0} rows";

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

    private void ReleaseTable()
    {
        if (ResultsTable is not { } table)
        {
            return;
        }

        table.SelectionChanged -= OnSelectionChanged;

        TableHost.Child = null;

        table.ItemsSource = null;

        ResultsTable = null;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ResultsTable))
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

    private static GridLength GetColumnWidth(ResultColumn column)
    {
        var width = Type.GetTypeCode(column.ClrType) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 => 60,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => 90,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => 100,
            TypeCode.DateTime => 160,
            _ => 140
        };

        return new GridLength(column.Width ?? Math.Max(width, HeaderLength(column) * 7 + 24));
    }

    private static object CreateHeader(ResultColumn column)
    {
        if (column.TypeName is null && column.Detail is null)
        {
            return column.Name;
        }

        var header = new TextBlock();

        header.Inlines.Add(new Run { Text = column.Name });

        if (column.TypeName is { } typeName)
        {
            header.Inlines.Add(new Run
            {
                Text = $"  {typeName}",
                FontFamily = Resource<FontFamily>("MonospaceFontFamily"),
                Foreground = Resource<Brush>("CppClassBrush")
            });
        }

        if (column.Detail is { } detail)
        {
            header.Inlines.Add(new Run { Text = $"  {detail}", Foreground = Resource<Brush>("TextFillColorTertiaryBrush") });
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
}