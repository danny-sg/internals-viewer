using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class MemoryReferenceGraph(List<MemoryReferenceGraphNode> nodes,
                                         List<MemoryReferenceGraphEdge> edges,
                                         double width,
                                         double height,
                                         double nodeWidth,
                                         double nodeHeight,
                                         bool isTruncated)
{
    public List<MemoryReferenceGraphNode> Nodes { get; } = nodes;

    public List<MemoryReferenceGraphEdge> Edges { get; } = edges;

    public double Width { get; } = width;

    public double Height { get; } = height;

    public double NodeWidth { get; } = nodeWidth;

    public double NodeHeight { get; } = nodeHeight;

    public bool IsTruncated { get; } = isTruncated;
}
