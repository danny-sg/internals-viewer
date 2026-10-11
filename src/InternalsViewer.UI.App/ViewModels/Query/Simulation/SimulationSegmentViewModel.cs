using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Query.Simulation;

namespace InternalsViewer.UI.App.ViewModels.Query.Simulation;

public sealed partial class SimulationSegmentViewModel : ObservableObject
{
    private const string NoColumns = "(No grouping columns)";

    [ObservableProperty]
    private long _segments;

    public SimulationSegmentViewModel(IReadOnlyList<string> groupBy)
    {
        GroupBy = groupBy;

        Columns = groupBy.Count > 0 ? groupBy : [NoColumns];

        Rows.Add(new SimulationSegmentRow("Current key", Columns.Count));
        Rows.Add(new SimulationSegmentRow("Row key", Columns.Count));
    }

    public ObservableCollection<SimulationSegmentRow> Rows { get; } = [];

    public IReadOnlyList<string> GroupBy { get; }

    /// <summary>
    /// One column per grouping column, or a single placeholder when the window has none
    /// </summary>
    public IReadOnlyList<string> Columns { get; }

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
