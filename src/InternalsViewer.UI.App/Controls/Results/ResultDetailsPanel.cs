using System;
using System.Linq;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Results;

internal static class ResultDetailsPanel
{
    private const double FontSize = 12;

    private static readonly Thickness HeaderPadding = new(12, 6, 16, 6);

    private static readonly Thickness HeaderBorder = new(0, 0, 0, 1);

    public static void Fill(Panel host, ResultDetails details, Action<RowBufferPage>? pagePointerClicked)
    {
        host.Children.Clear();

        host.Children.Add(new Border
        {
            Padding = HeaderPadding,
            BorderBrush = AppResources.Get<Brush>("CardStrokeColorDefaultBrush"),
            BorderThickness = HeaderBorder,
            Child = new TextBlock { Text = details.Title, FontSize = FontSize, VerticalAlignment = VerticalAlignment.Center }
        });

        if (details.Message is { } message)
        {
            host.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = FontSize,
                Padding = HeaderPadding,
                Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush")
            });

            return;
        }

        var grid = new ResultsGridControl
        {
            ResultSet = CreateResultSet(details),
            IsStatusBarVisible = false
        };

        if (pagePointerClicked is not null)
        {
            grid.PagePointerClicked += (_, pointer) => pagePointerClicked(pointer);
        }

        host.Children.Add(grid);
    }

    private static QueryResultSet CreateResultSet(ResultDetails details)
        => new()
        {
            Columns = [.. details.Columns.Select((c, ordinal) => new ResultColumn(ordinal, c.Label, typeof(string), true))],
            Rows = [new ResultRow<long>([.. details.Columns.Select(c => c.Value)])]
        };
}
