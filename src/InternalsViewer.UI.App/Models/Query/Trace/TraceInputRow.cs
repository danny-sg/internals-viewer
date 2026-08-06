using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed partial class TraceInputRow(int sourceNodeId, string name) : ObservableObject
{
    public int SourceNodeId { get; } = sourceNodeId;

    public string Name { get; } = name;

    public string Label { get; init; } = "";

    public Brush? Blob { get; init; }

    public ImageSource? Icon { get; init; }

    public bool HasRowCount { get; init; } = true;

    [ObservableProperty]
    private string _rowCount = "0";
}
