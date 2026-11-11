using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.UI.App.Models.Query.Trace.Lineage;

public sealed class LineageGraph(int nodeId, PlanOutputColumn column, Action<ulong> open)
{
    public const double BoxWidth = 460;

    public const double HeaderHeight = 40;

    public const double RowHeight = 22;

    public const double ColumnGap = 56;

    public event EventHandler? Changed;

    public event EventHandler? Rebuilt;

    public int NodeId { get; } = nodeId;

    public PlanOutputColumn Column { get; } = column;

    public IReadOnlyList<PlanOutputLink> Links { get; private set; } = [];

    public List<LineageBox> Boxes { get; private set; } = [];

    public double Width { get; private set; }

    public double Height { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public int Version { get; set; }

    public void Open(ulong address) => open(address);

    public void SetStructure(IReadOnlyList<PlanOutputLink> links, List<LineageBox> boxes, double width, double height, string key)
    {
        Links = links;

        Boxes = boxes;

        Width = width;

        Height = height;

        Key = key;

        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
