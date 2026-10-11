using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Simulation;

public sealed partial class SimulationSegmentRow(string name, int columns) : ObservableObject
{
    [ObservableProperty]
    private bool _isDifferent;

    public string Name { get; } = name;

    public ObservableCollection<SimulationSegmentCell> Cells { get; } = [.. Enumerable.Range(0, columns).Select(_ => new SimulationSegmentCell())];

    public void Fill(IReadOnlyList<string> values)
    {
        for (var index = 0; index < Cells.Count; index++)
        {
            Cells[index].Value = index < values.Count ? values[index] : string.Empty;
        }
    }
}

public sealed partial class SimulationSegmentCell : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;
}
