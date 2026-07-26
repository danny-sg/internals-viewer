using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Results;

public sealed partial class ResultsGridControl : UserControl
{
    public static readonly DependencyProperty ResultSetProperty =
        DependencyProperty.Register(
            nameof(ResultSet),
            typeof(QueryResultSet),
            typeof(ResultsGridControl),
            new PropertyMetadata(null, OnResultSetChanged));

    public static readonly DependencyProperty SelectedRowProperty =
        DependencyProperty.Register(
            nameof(SelectedRow),
            typeof(ResultRow<long>),
            typeof(ResultsGridControl),
            new PropertyMetadata(null));

    public QueryResultSet? ResultSet
    {
        get => (QueryResultSet?)GetValue(ResultSetProperty);
        set => SetValue(ResultSetProperty, value);
    }

    public ResultRow<long>? SelectedRow
    {
        get => (ResultRow<long>?)GetValue(SelectedRowProperty);
        set => SetValue(SelectedRowProperty, value);
    }

    private Size _lastKnownSize = new(800, 600);

    private int _realizedRows;

    public ResultsGridControl()
    {
        InitializeComponent();

        Loaded += (_, _) => TabDiagnostics.Log(nameof(ResultsGridControl), "Loaded");

        Unloaded += (_, _) => TabDiagnostics.Log(nameof(ResultsGridControl), "Unloaded");

        SizeChanged += (_, e) =>
        {
            if (e.NewSize is { Width: > 0, Height: > 0 })
            {
                _lastKnownSize = e.NewSize;
            }
        };

        ResultsDataGrid.LoadingRow += (_, _) =>
        {
            _realizedRows++;

            if (_realizedRows == 1)
            {
                TabDiagnostics.Log(nameof(ResultsGridControl), "First row realized");
            }
        };
    }

    private static void OnResultSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ResultsGridControl)d;
        
        control.SelectedRow = null;
        control.Rebuild();
    }

    private void Rebuild()
    {
        var start = TabDiagnostics.Start();

        _realizedRows = 0;

        ResultsDataGrid.Columns.Clear();
        ResultsDataGrid.ItemsSource = null;

        if (ResultSet is not { Columns: var columns, Rows: var rows })
        {
            StatusText.Text = string.Empty;
            return;
        }

        foreach (var column in columns)
        {
            ResultsDataGrid.Columns.Add(new ResultCellColumn(column.Ordinal)
            {
                Header = column.Name,
                BackgroundColour = column.BackgroundColour,
                Width = GetColumnWidth(column)
            });
        }

        ResultsDataGrid.ItemsSource = rows;

        StatusText.Text = rows.Count == 1 ? "1 row" : $"{rows.Count:N0} rows";

        TabDiagnostics.LogElapsed(nameof(ResultsGridControl), $"Rebuild ({rows.Count} rows, {columns.Count} cols)", start);

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

        var start = TabDiagnostics.Start();

        var size = GetWarmSize();

        ResultsDataGrid.Measure(size);

        TabDiagnostics.LogElapsed(nameof(ResultsGridControl),
                                  $"Warm at {size.Width:F0}x{size.Height:F0} "
                                  + $"({_realizedRows} rows realised, desired height {ResultsDataGrid.DesiredSize.Height:F0})",
                                  start);
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

    private static DataGridLength GetColumnWidth(ResultColumn column)
    {
        var width = Type.GetTypeCode(column.ClrType) switch
        {
            TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 => 60,
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => 90,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => 100,
            TypeCode.DateTime => 160,
            _ => 140
        };

        return new DataGridLength(Math.Max(width, column.Name.Length * 7 + 24));
    }
}