using InternalsViewer.Query.Results;
using Microsoft.UI.Xaml.Controls;

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

    public ResultsGridControl()
    {
        InitializeComponent();
    }

    private static void OnResultSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ResultsGridControl)d).Rebuild();

    private void Rebuild()
    {
        ResultsDataGrid.Columns.Clear();
        ResultsDataGrid.ItemsSource = null;

        if (ResultSet is not { Columns: var columns, Rows: var rows })
        {
            StatusText.Text = string.Empty;
            return;
        }

        foreach (var column in columns)
        {
            ResultsDataGrid.Columns.Add(new ResultCellColumn(column.Ordinal) { Header = column.Name });
        }

        ResultsDataGrid.ItemsSource = rows;

        StatusText.Text = rows.Count == 1 ? "1 row" : $"{rows.Count:N0} rows";
    }
}