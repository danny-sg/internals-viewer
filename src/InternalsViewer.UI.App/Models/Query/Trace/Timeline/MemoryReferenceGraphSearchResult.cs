namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryReferenceGraphSearchResult(MemoryReferenceGraphNode node, string name, string address)
{
    public MemoryReferenceGraphNode Node { get; } = node;

    public string Name { get; } = name;

    public string Address { get; } = address;
}
