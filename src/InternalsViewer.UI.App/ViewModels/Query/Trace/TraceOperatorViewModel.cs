using System;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceOperatorViewModel(int nodeId, string title, string description) : ObservableObject
{
    public int NodeId { get; } = nodeId;

    public string Title { get; } = title;

    /// <summary>
    /// What the operator matches on, which is the one thing about it that is not visible in its inputs
    /// </summary>
    public string Description { get; } = description;

    public Uri? Icon { get; set; }

    public string Heading { get; set; } = "";

    public string Subheading { get; set; } = "";

    public JoinDecision? JoinRule { get; set; }

    public TracePane OuterTop { get; set; } = TracePane.Empty;

    public TracePane OuterBottom { get; set; } = TracePane.Empty;

    public TracePane InnerTop { get; set; } = TracePane.Empty;

    public TracePane InnerBottom { get; set; } = TracePane.Empty;

    public TraceRowStreamViewModel Output { get; } = new();

    public event Action<int>? ActivationRequested;

    public void RequestActivation(int targetNodeId) => ActivationRequested?.Invoke(targetNodeId);
}
