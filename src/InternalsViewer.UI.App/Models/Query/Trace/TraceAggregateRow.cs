using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed partial class TraceAggregateRow(string column, string expression) : ObservableObject
{
    [ObservableProperty]
    private string _value = "NULL";

    public string Column { get; } = column;

    public string Expression { get; } = expression;
}
