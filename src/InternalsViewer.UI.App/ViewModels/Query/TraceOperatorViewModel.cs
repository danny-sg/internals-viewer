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
public sealed partial class TraceOperatorViewModel(int nodeId, string title, string description) : ObservableObject
{
    public int NodeId { get; } = nodeId;

    public string Title { get; } = title;

    /// <summary>
    /// What the operator matches on, which is the one thing about it that is not visible in its inputs
    /// </summary>
    public string Description { get; } = description;

    /// <summary>
    /// The input the operator reads first, either the object it scans or the results of the operator below it
    /// </summary>
    public TracePane OuterTop { get; set; } = TracePane.Empty;

    /// <summary>
    /// What the outer input holds, which is the hash table where the operator is a hash match and its rows otherwise
    /// </summary>
    public TracePane OuterBottom { get; set; } = TracePane.Empty;

    public TracePane InnerTop { get; set; } = TracePane.Empty;

    public TracePane InnerBottom { get; set; } = TracePane.Empty;

    public ObservableCollection<IndexRecordModel> Results { get; } = [];

    /// <summary>
    /// Names the pane these rows fill, which is the operator above reading them rather than this one producing them
    /// </summary>
    public string InputStreamLabel => Results.Count > 0 ? $"Input Stream ({Results.Count:N0})" : "Input Stream";

    public void Clear()
    {
        Results.Clear();

        OnPropertyChanged(nameof(InputStreamLabel));
    }

    public void Add(IndexRecordModel row)
    {
        Results.Add(row);

        OnPropertyChanged(nameof(InputStreamLabel));
    }
}
