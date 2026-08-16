using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed partial class TraceAggregateRow(string column, string expression) : ObservableObject
{
    public string Column { get; } = column;

    public string Expression { get; } = expression;

    [ObservableProperty]
    private string _value = "NULL";
}
