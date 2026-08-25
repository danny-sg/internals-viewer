using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceAggregateViewModel : ObservableObject
{
    [ObservableProperty]
    private string _groupKey = string.Empty;

    [ObservableProperty]
    private long _groupRows;

    [ObservableProperty]
    private long _groups;

    public TraceAggregateViewModel(IReadOnlyList<AggregateColumn> columns, IReadOnlyList<string> groupBy)
    {
        GroupBy = groupBy;

        foreach (var column in groupBy)
        {
            Rows.Add(new TraceAggregateRow(column, "Group By"));
        }

        foreach (var column in columns)
        {
            Rows.Add(new TraceAggregateRow(column.Column, column.ToText()));
        }
    }

    public ObservableCollection<TraceAggregateRow> Rows { get; } = [];

    public IReadOnlyList<string> GroupBy { get; }

    public bool IsGrouped => GroupBy.Count > 0;

    public string GroupHeading => IsGrouped ? $"Group by {string.Join(", ", GroupBy)}" : "(No group by)";

    public void Sync(IReadOnlyList<AggregateValue> groupValues,
                     IReadOnlyList<AggregateValue> values,
                     string groupKey,
                     long groupRows,
                     long groups)
    {
        GroupKey = groupKey;
        GroupRows = groupRows;
        Groups = groups;

        Fill(0, groupValues);

        Fill(GroupBy.Count, values);
    }

    public void Reset()
    {
        GroupKey = string.Empty;
        GroupRows = 0;
        Groups = 0;

        foreach (var row in Rows)
        {
            row.Value = "NULL";
        }
    }

    private void Fill(int offset, IReadOnlyList<AggregateValue> values)
    {
        for (var index = 0; index < values.Count && offset + index < Rows.Count; index++)
        {
            Rows[offset + index].Value = values[index].Value;
        }
    }
}
