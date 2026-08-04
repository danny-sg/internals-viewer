using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceOperatorViewModel(int nodeId, string title, string description) : ObservableObject
{
    public int NodeId { get; } = nodeId;

    public string Title { get; } = title;

    /// <summary>
    /// What the operator matches on, which is the one thing about it that is not visible in its inputs
    /// </summary>
    public string Description { get; } = description;

    public TracePane OuterTop { get; set; } = TracePane.Empty;

    public TracePane OuterBottom { get; set; } = TracePane.Empty;

    public TracePane InnerTop { get; set; } = TracePane.Empty;

    public TracePane InnerBottom { get; set; } = TracePane.Empty;

    public TraceRowStreamViewModel Output { get; } = new("Output");
}
