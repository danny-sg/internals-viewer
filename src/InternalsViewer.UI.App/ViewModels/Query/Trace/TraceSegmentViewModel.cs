using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceSegmentViewModel : ObservableObject
{
    private const string NoColumns = "(No grouping columns)";

    public TraceSegmentViewModel(IReadOnlyList<string> groupBy)
    {
        GroupBy = groupBy;

        Columns = groupBy.Count > 0 ? groupBy : [NoColumns];

        Rows.Add(new TraceSegmentRow("Current key", Columns.Count));
        Rows.Add(new TraceSegmentRow("Row key", Columns.Count));
    }

    public ObservableCollection<TraceSegmentRow> Rows { get; } = [];

    public IReadOnlyList<string> GroupBy { get; }

    /// <summary>
    /// One column per grouping column, or a single placeholder when the window has none
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

    [ObservableProperty]
    private long _segments;

    public void Sync(IReadOnlyList<string> currentKey, IReadOnlyList<string> rowKey, long segments)
    {
        Rows[0].Fill(currentKey);
        Rows[1].Fill(rowKey);

        var isDifferent = !currentKey.SequenceEqual(rowKey);

        foreach (var row in Rows)
        {
            row.IsDifferent = isDifferent;
        }

        Segments = segments;
    }

    public void Reset()
    {
        Segments = 0;

        foreach (var row in Rows)
        {
            row.Fill([]);

            row.IsDifferent = false;
        }
    }
}
