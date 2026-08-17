using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed partial class TraceSegmentRow(string name, int columns) : ObservableObject
{
    public string Name { get; } = name;

    public ObservableCollection<TraceSegmentCell> Cells { get; } = [.. Enumerable.Range(0, columns).Select(_ => new TraceSegmentCell())];

    [ObservableProperty]
    private bool _isDifferent;

    public void Fill(IReadOnlyList<string> values)
    {
        for (var index = 0; index < Cells.Count; index++)
        {
            Cells[index].Value = index < values.Count ? values[index] : string.Empty;
        }
    }
}

public sealed partial class TraceSegmentCell : ObservableObject
{
    [ObservableProperty]
    private string _value = string.Empty;
}
