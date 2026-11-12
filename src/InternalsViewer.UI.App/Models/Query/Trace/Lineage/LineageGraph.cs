using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.Models.Query.Trace.Lineage;

public sealed class LineageGraph(int nodeId, PlanOutputColumn column, PlanOutputPath path, Action<ulong> open)
{
    public const double BoxWidth = 230;

    public const double HeaderHeight = 40;

    public const double LineHeight = 18;

    public const double RowHeight = LineHeight * 2;

    public const double ColumnGap = 56;

    public event EventHandler? Changed;

    public event EventHandler? Rebuilt;

    public int NodeId { get; } = nodeId;

    public PlanOutputColumn Column { get; } = column;

    public PlanOutputPath Path { get; } = path;

    public List<LineageBox> Boxes { get; private set; } = [];

    public double Width { get; private set; }

    public double Height { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; private set; } = [];

    public int Version { get; set; }

    public void Open(ulong address) => open(address);

    public void SetStructure(List<LineageBox> boxes,
                             double width,
                             double height,
                             string key,
                             IReadOnlyList<string> warnings)
    {
        Boxes = boxes;

        Width = width;

        Height = height;

        Key = key;

        Warnings = warnings;

        Rebuilt?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
