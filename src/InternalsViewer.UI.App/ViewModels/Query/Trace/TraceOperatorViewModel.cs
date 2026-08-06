using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.UI.App.Models.Query.Trace;

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

    public bool HasOutputPane { get; set; } = true;

    public bool IsOutputDefaultVisible { get; set; }

    public bool IsJoinLayout { get; set; }

    public TraceBlobPalette? BlobPalette { get; set; }

    public ObservableCollection<TraceInputRow> InputRows { get; } = [];

    public ObservableCollection<TraceStateItem> StateItems { get; } = [];

    public TracePane MainPane { get; set; } = TracePane.Empty;

    public TracePane OuterTop { get; set; } = TracePane.Empty;

    public TracePane OuterBottom { get; set; } = TracePane.Empty;

    public TracePane InnerTop { get; set; } = TracePane.Empty;

    public TracePane InnerBottom { get; set; } = TracePane.Empty;

    public TraceRowStreamViewModel Output { get; } = new();

    public event Action<int>? ActivationRequested;

    public void RequestActivation(int targetNodeId) => ActivationRequested?.Invoke(targetNodeId);

    public void SetState(string name, string value)
    {
        foreach (var item in StateItems)
        {
            if (item.Name == name)
            {
                item.Value = value;

                return;
            }
        }
    }
}
