using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Simulation;

public sealed partial class SimulationAggregateRow(string column, string expression) : ObservableObject
{
    [ObservableProperty]
    private string _value = "NULL";

    public string Column { get; } = column;

    public string Expression { get; } = expression;
}
