using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;

namespace InternalsViewer.UI.App.Models.Query.Trace.Lineage;

public sealed class LineageBox(string title, string subtitle, ulong address, PlanOutputLinkType? type, LineageBox? parent)
{
    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public ulong Address { get; } = address;

    public PlanOutputLinkType? Type { get; } = type;

    public LineageBox? Parent { get; } = parent;

    public int Column { get; } = parent is null ? 0 : parent.Column + 1;

    public List<LineageRow> Rows { get; } = [];

    public double X { get; set; }

    public double Y { get; set; }

    public double Height => LineageGraph.HeaderHeight + Rows.Count * LineageGraph.RowHeight;
}
