using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// The rows one operator produced, which is what the operator above it reads
/// </summary>
/// <remarks>
/// An operator in the middle of a tree has no table of its own to show, so its results are the only thing it contributes to the trace.
/// Rows are matched to it by the id its steps carry, because a nested operator's results flow up through its parent's step stream and
/// filtering on the kind of step alone would attribute them to whichever operator was being watched.
/// </remarks>
public sealed partial class TraceOperatorViewModel(int nodeId, string title) : ObservableObject
{
    public int NodeId { get; } = nodeId;

    public string Title { get; } = title;

    public ObservableCollection<IndexRecordModel> Results { get; } = [];

    public string ResultsLabel => Results.Count > 0 ? $"Results ({Results.Count:N0})" : "Results";

    public void Clear()
    {
        Results.Clear();

        OnPropertyChanged(nameof(ResultsLabel));
    }

    public void Add(IndexRecordModel row)
    {
        Results.Add(row);

        OnPropertyChanged(nameof(ResultsLabel));
    }
}
